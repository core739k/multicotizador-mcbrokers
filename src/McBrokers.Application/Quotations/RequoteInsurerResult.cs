using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Quotations;

public sealed record RequoteInsurerCommand(
    Guid QuotationId,
    Guid InsurerId,
    Guid? OverrideVehicleMasterId,
    ValuationType? OverrideValuation,
    decimal? OverrideDMPct,
    decimal? OverrideRTPct,
    decimal? OverrideGMO);

public sealed record RequoteInsurerOutcome(
    Guid ResultId,
    int Version,
    QuotationInsurerStatus Status,
    decimal? PremiumTotal,
    decimal? PremiumNet,
    decimal? Tax,
    decimal? Fees,
    string? ErrorCode,
    string? ErrorMessage);

// Re-cotización síncrona de UNA aseguradora con overrides aplicados sobre los
// parámetros base de la Quotation. Vive aparte de ProcessQuotation (que es
// async + worker + N aseguradoras). Comparte el "build request" via
// ProcessQuotation pero replica las partes que no son DRYizables sin mover
// más cosas a Application.
public sealed class RequoteInsurerResult
{
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;
    private readonly IVehicleInsurerMappingRepository _mappings;
    private readonly IInsurerRepository _insurers;
    private readonly IInsurerConfigRepository _configs;
    private readonly IInsurerPackageMappingRepository _packageMappings;
    private readonly IInsurerCredentialProvider _credentials;
    private readonly IAxaDxnConfigRepository _axaDxnConfigs;
    private readonly IBlobStore _blob;
    private readonly IEnumerable<IInsurerAdapter> _adapters;
    private readonly IClock _clock;
    private readonly IKnownInsurerErrorLookup _errorLookup;

    public RequoteInsurerResult(
        IQuotationRepository quotations,
        IVehicleMasterRepository vehicles,
        IVehicleInsurerMappingRepository mappings,
        IInsurerRepository insurers,
        IInsurerConfigRepository configs,
        IInsurerPackageMappingRepository packageMappings,
        IInsurerCredentialProvider credentials,
        IAxaDxnConfigRepository axaDxnConfigs,
        IBlobStore blob,
        IEnumerable<IInsurerAdapter> adapters,
        IClock clock,
        IKnownInsurerErrorLookup errorLookup)
    {
        _quotations = quotations;
        _vehicles = vehicles;
        _mappings = mappings;
        _insurers = insurers;
        _configs = configs;
        _packageMappings = packageMappings;
        _credentials = credentials;
        _axaDxnConfigs = axaDxnConfigs;
        _blob = blob;
        _adapters = adapters;
        _clock = clock;
        _errorLookup = errorLookup;
    }

    public async Task<Result<RequoteInsurerOutcome>> ExecuteAsync(
        RequoteInsurerCommand cmd, CancellationToken cancellationToken)
    {
        var quotation = await _quotations.GetByIdAsync(cmd.QuotationId, cancellationToken).ConfigureAwait(false);
        if (quotation is null)
        {
            return Result<RequoteInsurerOutcome>.Failure($"Quotation '{cmd.QuotationId}' not found.");
        }

        var insurer = await _insurers.GetByIdAsync(cmd.InsurerId, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
        {
            return Result<RequoteInsurerOutcome>.Failure($"Insurer '{cmd.InsurerId}' not found.");
        }

        if (!insurer.IsEnabled)
        {
            return Result<RequoteInsurerOutcome>.Failure($"Insurer '{insurer.Name}' is disabled.");
        }

        var prior = quotation.CurrentResultFor(insurer.Id);
        if (prior is null)
        {
            return Result<RequoteInsurerOutcome>.Failure(
                $"No prior result for insurer '{insurer.Name}' — re-quoting requires an initial quote.");
        }

        var adapter = _adapters.FirstOrDefault(a => a.Code == insurer.Code);
        if (adapter is null)
        {
            return Result<RequoteInsurerOutcome>.Failure($"No adapter registered for insurer '{insurer.Code}'.");
        }

        // Aplicar overrides sobre los valores base de la Quotation.
        var vehicleId = cmd.OverrideVehicleMasterId ?? quotation.VehicleMasterId;
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return Result<RequoteInsurerOutcome>.Failure($"Vehicle master '{vehicleId}' not found.");
        }

        var mappings = await _mappings.ListByMasterAsync(vehicle.Id, cancellationToken).ConfigureAwait(false);
        var insurerMapping = mappings.FirstOrDefault(m =>
            m.InsurerId == insurer.Id && m.ReviewState == ReviewState.Approved);
        if (insurerMapping is null)
        {
            return Result<RequoteInsurerOutcome>.Failure(
                $"No approved AMIS mapping for {vehicle.Year} {vehicle.Brand} {vehicle.Model} on {insurer.Name}.");
        }

        var config = await _configs.GetAsync(insurer.Id, cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            return Result<RequoteInsurerOutcome>.Failure($"{insurer.Name} has no environment config.");
        }

        var creds = await _credentials.ResolveAsync(config.KeyVaultSecretName, cancellationToken).ConfigureAwait(false);
        var packageCode = await _packageMappings
            .GetExternalCodeAsync(insurer.Id, quotation.Package, cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        var customer = ParseCustomerSnapshot(quotation.CustomerSnapshotJson);
        var deducibles = ApplyDeducibleOverrides(customer.Deductibles, cmd);
        var valuation = cmd.OverrideValuation ?? quotation.ValuationType;

        InsurerBusinessConfig? businessConfig = null;
        if (insurer.Code == InsurerCode.AxaDxn)
        {
            businessConfig = await BuildAxaDxnBusinessConfigAsync(insurer.Id, cancellationToken).ConfigureAwait(false);
            if (businessConfig is null)
            {
                return Result<RequoteInsurerOutcome>.Failure(
                    "AXA DXN no tiene AxaDxnConfig capturado. Completa /Admin/Insurers/{id}.");
            }
        }

        var request = new InsurerQuoteRequest(
            CorrelationId: quotation.CorrelationId,
            Credentials: new InsurerCredentials(creds.Username, creds.Password, config.BusinessNumber),
            Connection: new InsurerConnectionConfig(config.EndpointUrl, config.TimeoutSeconds, config.MaxRetries),
            Vehicle: new VehicleSelection(vehicle.Year, vehicle.Brand, vehicle.Model, vehicle.Version, insurerMapping.ExternalClave),
            Package: quotation.Package,
            PackageExternalCode: packageCode,
            PaymentMode: quotation.PaymentMode,
            ValuationType: valuation,
            SumInsured: quotation.SumInsured,
            Deductibles: deducibles,
            Contractor: customer.Contractor,
            HabitualDriver: customer.HabitualDriver,
            PostalCode: quotation.PostalCode,
            BusinessConfig: businessConfig);

        var outcome = await adapter.QuoteAsync(request, cancellationToken).ConfigureAwait(false);

        var (reqBlob, resBlob) = await PersistBlobsAsync(
            quotation.CorrelationId, insurer.Code, outcome, cancellationToken).ConfigureAwait(false);

        var overridesSnapshot = new QuotationInsurerOverrides(
            VehicleMasterId: cmd.OverrideVehicleMasterId,
            Valuation: cmd.OverrideValuation,
            MaterialDamagesDeductiblePct: cmd.OverrideDMPct,
            RobberyDeductiblePct: cmd.OverrideRTPct,
            MedicalExpensesSumInsured: cmd.OverrideGMO);

        var nextVersion = prior.Version + 1;
        QuotationInsurerResult newResult;
        if (outcome is InsurerQuoteOutcome.Success s)
        {
            newResult = QuotationInsurerResult.SucceededRequoteResult(
                quotation.Id, insurer.Id,
                s.Response.PremiumTotal, s.Response.PremiumNet, s.Response.Tax, s.Response.Fees,
                s.Response.LatencyMs, s.Response.ExternalQuoteRef,
                reqBlob, resBlob, _clock.UtcNow,
                version: nextVersion, overrides: overridesSnapshot).Value;
        }
        else if (outcome is InsurerQuoteOutcome.Failure f)
        {
            var known = await _errorLookup.FindAsync(insurer.Id, f.Error.ExternalCode, cancellationToken).ConfigureAwait(false);
            var humanMessage = known?.HumanMessage ?? f.Error.ExternalMessage;
            var category = known?.Category ?? f.Error.Category;

            newResult = QuotationInsurerResult.FailedRequoteResult(
                quotation.Id, insurer.Id,
                f.Error.Status, category,
                f.Error.ExternalCode, humanMessage,
                f.Error.LatencyMs, reqBlob, resBlob, _clock.UtcNow,
                version: nextVersion, overrides: overridesSnapshot).Value;
        }
        else
        {
            throw new InvalidOperationException("Unknown outcome type.");
        }

        var superseded = quotation.SupersedeAndRecord(newResult);
        if (!superseded.IsSuccess)
        {
            return Result<RequoteInsurerOutcome>.Failure(superseded.Error);
        }

        await _quotations.AppendResultAsync(newResult, cancellationToken).ConfigureAwait(false);
        await _quotations.UpdateAsync(quotation, cancellationToken).ConfigureAwait(false);

        return Result<RequoteInsurerOutcome>.Success(new RequoteInsurerOutcome(
            ResultId: newResult.Id,
            Version: newResult.Version,
            Status: newResult.Status,
            PremiumTotal: newResult.PremiumTotal,
            PremiumNet: newResult.PremiumNet,
            Tax: newResult.Tax,
            Fees: newResult.Fees,
            ErrorCode: newResult.ErrorCode,
            ErrorMessage: newResult.ErrorMessageHuman));
    }

    private static DeductiblesAndSums ApplyDeducibleOverrides(DeductiblesAndSums baseDed, RequoteInsurerCommand cmd) =>
        new(
            MaterialDamagesDeductiblePct: cmd.OverrideDMPct ?? baseDed.MaterialDamagesDeductiblePct,
            RobberyDeductiblePct: cmd.OverrideRTPct ?? baseDed.RobberyDeductiblePct,
            MedicalExpensesSumInsured: cmd.OverrideGMO ?? baseDed.MedicalExpensesSumInsured,
            CivilLiabilitySumInsured: baseDed.CivilLiabilitySumInsured);

    private async Task<AxaDxnAdapterConfig?> BuildAxaDxnBusinessConfigAsync(Guid insurerId, CancellationToken ct)
    {
        var snapshot = await _axaDxnConfigs.GetByInsurerIdAsync(insurerId, ct).ConfigureAwait(false);
        if (snapshot is null) return null;

        var business = snapshot.Businesses.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.PolizaAutos))
                    ?? snapshot.Businesses.FirstOrDefault();

        return new AxaDxnAdapterConfig(
            Usuario: snapshot.Config.Usuario,
            Password: snapshot.Config.Password,
            Tarifa: snapshot.Config.Tarifa,
            TarifaPickup: snapshot.Config.TarifaPickup,
            Descuento: snapshot.Config.Descuento,
            DescuentoPickup: snapshot.Config.DescuentoPickup,
            MesPolizaDefault: snapshot.Config.MesPolizaDefault,
            SelectedBusinessName: business?.Nombre.ToString() ?? string.Empty,
            PolizaAutos: business?.PolizaAutos,
            PolizaPickup: business?.PolizaPickup,
            BusinessMes: business?.Mes ?? snapshot.Config.MesPolizaDefault,
            CopsisD4Key: snapshot.Config.CopsisD4Key,
            CopsisB: snapshot.Config.CopsisB);
    }

    private async Task<(string? RequestRef, string? ResponseRef)> PersistBlobsAsync(
        string correlationId, InsurerCode insurerCode, InsurerQuoteOutcome outcome, CancellationToken ct)
    {
        var (requestXml, responseXml) = outcome switch
        {
            InsurerQuoteOutcome.Success s => (s.Response.RawRequest, s.Response.RawResponse),
            InsurerQuoteOutcome.Failure f => (f.Error.RawRequest ?? string.Empty, f.Error.RawResponse ?? string.Empty),
            _ => (string.Empty, string.Empty),
        };

        var metadata = new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["insurer"] = insurerCode.ToString(),
            ["requote"] = "true",
        };

        var reqRef = await _blob.WriteAsync(
            container: "xml-requests",
            blobName: $"{insurerCode}/{correlationId}-requote-{Guid.NewGuid():n}-request.xml",
            content: requestXml,
            metadata: metadata,
            cancellationToken: ct).ConfigureAwait(false);

        var resRef = await _blob.WriteAsync(
            container: "xml-responses",
            blobName: $"{insurerCode}/{correlationId}-requote-{Guid.NewGuid():n}-response.xml",
            content: responseXml,
            metadata: metadata,
            cancellationToken: ct).ConfigureAwait(false);

        return (reqRef, resRef);
    }

    private static ProcessQuotation.CustomerSnapshot ParseCustomerSnapshot(string json)
    {
        try
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProcessQuotation.CustomerSnapshot>(json, JsonOpts);
            return snapshot ?? ProcessQuotation.CustomerSnapshot.Empty;
        }
        catch
        {
            return ProcessQuotation.CustomerSnapshot.Empty;
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
