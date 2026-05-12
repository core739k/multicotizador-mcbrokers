using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;

namespace McBrokers.Application.Quotations;

/// <summary>
/// Caso de uso interno: lo invoca el worker para ejecutar la cotización contra
/// las aseguradoras habilitadas. Construye el InsurerQuoteRequest, llama al adapter,
/// persiste el resultado y los XMLs req/res en Blob.
/// </summary>
public sealed class ProcessQuotation
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

    public ProcessQuotation(
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

    public async Task ExecuteAsync(Guid quotationId, CancellationToken cancellationToken)
    {
        var quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quotation is null) return;

        var vehicle = await _vehicles.GetByIdAsync(quotation.VehicleMasterId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null) return;

        // F3: only GNP is enabled. Future fases: iterate over all enabled adapters
        // whose insurer has a valid mapping for this vehicle.
        var insurers = await _insurers.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var insurer in insurers.Where(i => i.IsEnabled))
        {
            var adapter = _adapters.FirstOrDefault(a => a.Code == insurer.Code);
            if (adapter is null) continue;

            await ProcessOneAsync(quotation, vehicle, insurer, adapter, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(
        Quotation quotation, VehicleMaster vehicle, Insurer insurer,
        IInsurerAdapter adapter, CancellationToken cancellationToken)
    {
        var startedAt = _clock.UtcNow;
        var mappings = await _mappings.ListByMasterAsync(vehicle.Id, cancellationToken).ConfigureAwait(false);
        var insurerMapping = mappings.FirstOrDefault(m =>
            m.InsurerId == insurer.Id && m.ReviewState == ReviewState.Approved);

        if (insurerMapping is null)
        {
            var notCovered = QuotationInsurerResult.FailedResult(
                quotation.Id, insurer.Id,
                QuotationInsurerStatus.NotCovered, ErrorCategory.Business,
                "NO_MAPPING",
                $"No hay clave AMIS aprobada para {vehicle.Year} {vehicle.Brand} {vehicle.Model} en {insurer.Name}.",
                latencyMs: 0,
                requestBlobRef: null, responseBlobRef: null,
                createdAt: _clock.UtcNow).Value;

            quotation.RecordResult(notCovered);
            await _quotations.AppendResultAsync(notCovered, cancellationToken).ConfigureAwait(false);
            await _quotations.UpdateAsync(quotation, cancellationToken).ConfigureAwait(false);
            return;
        }

        var config = await _configs.GetAsync(insurer.Id, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            var notConfigured = QuotationInsurerResult.FailedResult(
                quotation.Id, insurer.Id,
                QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                "NO_CONFIG",
                $"{insurer.Name} no tiene configuración de entorno.",
                latencyMs: 0, requestBlobRef: null, responseBlobRef: null,
                createdAt: _clock.UtcNow).Value;
            quotation.RecordResult(notConfigured);
            await _quotations.AppendResultAsync(notConfigured, cancellationToken).ConfigureAwait(false);
            await _quotations.UpdateAsync(quotation, cancellationToken).ConfigureAwait(false);
            return;
        }

        var creds = await _credentials.ResolveAsync(config.KeyVaultSecretName, cancellationToken).ConfigureAwait(false);

        var packageCode = await _packageMappings
            .GetExternalCodeAsync(insurer.Id, quotation.Package, cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        var customer = ParseCustomerSnapshot(quotation.CustomerSnapshotJson);

        // POC AXA DXN: para esta aseguradora cargamos AxaDxnConfig + business resuelto y
        // lo pasamos como BusinessConfig tipado. Las otras 3 aseguradoras siguen el camino
        // viejo de Credentials hasta que se expanda el patrón.
        InsurerBusinessConfig? businessConfig = null;
        if (insurer.Code == InsurerCode.AxaDxn)
        {
            businessConfig = await BuildAxaDxnBusinessConfigAsync(insurer.Id, cancellationToken)
                .ConfigureAwait(false);
            if (businessConfig is null)
            {
                var notConfigured = QuotationInsurerResult.FailedResult(
                    quotation.Id, insurer.Id,
                    QuotationInsurerStatus.Failed, ErrorCategory.Technical,
                    "NO_CONFIG",
                    "AXA DXN no tiene AxaDxnConfig capturado. Completa /Admin/Insurers/{id}.",
                    latencyMs: 0, requestBlobRef: null, responseBlobRef: null,
                    createdAt: _clock.UtcNow).Value;
                quotation.RecordResult(notConfigured);
                await _quotations.AppendResultAsync(notConfigured, cancellationToken).ConfigureAwait(false);
                await _quotations.UpdateAsync(quotation, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        var insurerRequest = new InsurerQuoteRequest(
            CorrelationId: quotation.CorrelationId,
            Credentials: new InsurerCredentials(creds.Username, creds.Password, config.BusinessNumber),
            Connection: new InsurerConnectionConfig(config.EndpointUrl, config.TimeoutSeconds, config.MaxRetries),
            Vehicle: new VehicleSelection(vehicle.Year, vehicle.Brand, vehicle.Model, vehicle.Version, insurerMapping.ExternalClave),
            Package: quotation.Package,
            PackageExternalCode: packageCode,
            PaymentMode: quotation.PaymentMode,
            ValuationType: quotation.ValuationType,
            SumInsured: quotation.SumInsured,
            Deductibles: customer.Deductibles,
            Contractor: customer.Contractor,
            HabitualDriver: customer.HabitualDriver,
            PostalCode: quotation.PostalCode,
            BusinessConfig: businessConfig);

        var outcome = await adapter.QuoteAsync(insurerRequest, cancellationToken).ConfigureAwait(false);

        var (requestBlobRef, responseBlobRef) = await PersistBlobsAsync(
            quotation.CorrelationId, insurer.Code, outcome, cancellationToken).ConfigureAwait(false);

        QuotationInsurerResult result;
        if (outcome is InsurerQuoteOutcome.Success s)
        {
            result = QuotationInsurerResult.SucceededResult(
                quotation.Id, insurer.Id,
                s.Response.PremiumTotal, s.Response.PremiumNet, s.Response.Tax, s.Response.Fees,
                s.Response.LatencyMs, s.Response.ExternalQuoteRef,
                requestBlobRef, responseBlobRef, _clock.UtcNow).Value;
        }
        else if (outcome is InsurerQuoteOutcome.Failure f)
        {
            // Buscar el mensaje administrable; si no hay match, usar el mensaje crudo del adapter.
            var known = await _errorLookup
                .FindAsync(insurer.Id, f.Error.ExternalCode, cancellationToken)
                .ConfigureAwait(false);
            var humanMessage = known?.HumanMessage ?? f.Error.ExternalMessage;
            var category = known?.Category ?? f.Error.Category;

            result = QuotationInsurerResult.FailedResult(
                quotation.Id, insurer.Id,
                f.Error.Status, category,
                f.Error.ExternalCode, humanMessage,
                f.Error.LatencyMs, requestBlobRef, responseBlobRef, _clock.UtcNow).Value;
        }
        else
        {
            throw new InvalidOperationException("Unknown outcome type.");
        }

        quotation.RecordResult(result);
        await _quotations.AppendResultAsync(result, cancellationToken).ConfigureAwait(false);
        await _quotations.UpdateAsync(quotation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AxaDxnAdapterConfig?> BuildAxaDxnBusinessConfigAsync(
        Guid insurerId, CancellationToken cancellationToken)
    {
        var snapshot = await _axaDxnConfigs.GetByInsurerIdAsync(insurerId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null) return null;

        // POC: el vendedor todavía no selecciona negocio al cotizar (futuro Quotation.SelectedBusiness).
        // Mientras tanto, elegimos el primer negocio que tenga PolizaAutos. Si ninguno tiene póliza,
        // tomamos el primero existente y dejamos PolizaAutos en null — el adapter falla con NO_POLIZA.
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
        };

        var reqRef = await _blob.WriteAsync(
            container: "xml-requests",
            blobName: $"{insurerCode}/{correlationId}-request.xml",
            content: requestXml,
            metadata: metadata,
            cancellationToken: ct).ConfigureAwait(false);

        var resRef = await _blob.WriteAsync(
            container: "xml-responses",
            blobName: $"{insurerCode}/{correlationId}-response.xml",
            content: responseXml,
            metadata: metadata,
            cancellationToken: ct).ConfigureAwait(false);

        return (reqRef, resRef);
    }

    private static CustomerSnapshot ParseCustomerSnapshot(string json)
    {
        try
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CustomerSnapshot>(json, JsonOpts);
            return snapshot ?? CustomerSnapshot.Empty;
        }
        catch
        {
            return CustomerSnapshot.Empty;
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public sealed record CustomerSnapshot(
        ContactInfo Contractor,
        DriverInfo HabitualDriver,
        DeductiblesAndSums Deductibles)
    {
        public static CustomerSnapshot Empty => new(
            new ContactInfo("N/A", "N/A", "N/A", "00000", Gender.Male, new DateOnly(1990, 1, 1)),
            new DriverInfo("00000", Gender.Male, new DateOnly(1990, 1, 1)),
            new DeductiblesAndSums(5m, 10m, 200000m, 3000000m));
    }
}

public interface IInsurerCredentialProvider
{
    Task<InsurerCredentialPair> ResolveAsync(string keyVaultSecretName, CancellationToken cancellationToken);
}

public sealed record InsurerCredentialPair(string Username, string Password);
