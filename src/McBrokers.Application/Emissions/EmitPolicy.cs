using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Emissions;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Emissions;

public sealed record EmitPolicyCommand(
    Guid QuotationInsurerResultId,
    EmissionCustomerSnapshot Customer);

public sealed record EmissionCustomerSnapshot(
    string FirstName,
    string LastNamePaternal,
    string LastNameMaternal,
    string Rfc,
    string Street,
    string ExteriorNumber,
    string? InteriorNumber,
    string Neighborhood,
    string City,
    string StateCode,
    string PostalCode,
    string Phone,
    string Email,
    string Plate,
    string EngineNumber,
    string SerialNumber);

public sealed record EmitPolicyResult(Guid EmissionId, string? PolicyNumber, EmissionStatus Status);

public sealed class EmitPolicy
{
    private readonly IEmissionRepository _emissions;
    private readonly IQuotationRepository _quotations;
    private readonly IVehicleMasterRepository _vehicles;
    private readonly IVehicleInsurerMappingRepository _mappings;
    private readonly IInsurerRepository _insurers;
    private readonly IInsurerConfigRepository _configs;
    private readonly IInsurerCredentialProvider _credentials;
    private readonly IBlobStore _blob;
    private readonly IPdfDownloader _pdfDownloader;
    private readonly IEnumerable<IInsurerAdapter> _adapters;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;
    private readonly IKnownInsurerErrorLookup _errorLookup;

    public EmitPolicy(
        IEmissionRepository emissions,
        IQuotationRepository quotations,
        IVehicleMasterRepository vehicles,
        IVehicleInsurerMappingRepository mappings,
        IInsurerRepository insurers,
        IInsurerConfigRepository configs,
        IInsurerCredentialProvider credentials,
        IBlobStore blob,
        IPdfDownloader pdfDownloader,
        IEnumerable<IInsurerAdapter> adapters,
        IAuditWriter audit,
        IClock clock,
        ICurrentAgentProvider currentAgent,
        IKnownInsurerErrorLookup errorLookup)
    {
        _emissions = emissions;
        _quotations = quotations;
        _vehicles = vehicles;
        _mappings = mappings;
        _insurers = insurers;
        _configs = configs;
        _credentials = credentials;
        _blob = blob;
        _pdfDownloader = pdfDownloader;
        _adapters = adapters;
        _audit = audit;
        _clock = clock;
        _currentAgent = currentAgent;
        _errorLookup = errorLookup;
    }

    public async Task<Result<EmitPolicyResult>> ExecuteAsync(
        EmitPolicyCommand command, CancellationToken cancellationToken)
    {
        // Idempotencia: si ya hay una Emission para este result, devolvemos esa.
        var existing = await _emissions
            .GetByQuotationResultAsync(command.QuotationInsurerResultId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null && existing.Status == EmissionStatus.Issued)
        {
            return Result<EmitPolicyResult>.Success(
                new EmitPolicyResult(existing.Id, existing.PolicyNumber, existing.Status));
        }

        // Recupera contexto: Quotation + Vehicle + Mapping + Insurer + Config + Adapter.
        var context = await BuildContextAsync(command.QuotationInsurerResultId, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess) return Result<EmitPolicyResult>.Failure(context.Error);

        var ctx = context.Value;

        var emission = existing ?? Emission.Start(
            command.QuotationInsurerResultId, _currentAgent.AgentId, _clock.UtcNow).Value;

        if (existing is null)
        {
            await _emissions.AddAsync(emission, cancellationToken).ConfigureAwait(false);
        }

        var emitRequest = BuildEmitRequest(command, ctx);

        var startedAt = _clock.UtcNow;
        var outcome = await ctx.Adapter.EmitAsync(emitRequest, cancellationToken).ConfigureAwait(false);
        var latency = (int)(_clock.UtcNow - startedAt).TotalMilliseconds;

        // Persistir attempt (1 attempt por ahora; retry/backoff lo agrega F5.5 con Polly).
        var attempt = EmissionAttempt.Create(
            emission.Id, attemptNumber: 1,
            outcome: outcome is InsurerEmitOutcome.Success ? "Succeeded" : "Failed",
            latencyMs: latency,
            errorCode: outcome is InsurerEmitOutcome.Failure f ? f.Error.ExternalCode : null,
            createdAt: _clock.UtcNow);
        if (attempt.IsSuccess)
        {
            await _emissions.AddAttemptAsync(attempt.Value, cancellationToken).ConfigureAwait(false);
        }

        if (outcome is InsurerEmitOutcome.Failure failure)
        {
            // Enriquecer con mensaje administrable si existe.
            var known = await _errorLookup
                .FindAsync(ctx.Insurer.Id, failure.Error.ExternalCode, cancellationToken)
                .ConfigureAwait(false);
            var humanMessage = known?.HumanMessage ?? failure.Error.ExternalMessage;

            emission.MarkFailed($"[{failure.Error.ExternalCode}] {humanMessage}");
            await _emissions.UpdateAsync(emission, cancellationToken).ConfigureAwait(false);

            await _audit.WriteAsync(
                action: "Emission.Failed",
                entityType: "Emission",
                entityId: emission.Id.ToString(),
                payload: new { command.QuotationInsurerResultId, failure.Error.ExternalCode, humanMessage },
                cancellationToken).ConfigureAwait(false);

            return Result<EmitPolicyResult>.Success(
                new EmitPolicyResult(emission.Id, null, emission.Status));
        }

        var success = (InsurerEmitOutcome.Success)outcome;
        string? pdfBlobRef = null;

        if (!string.IsNullOrWhiteSpace(success.Response.PdfDownloadUrl))
        {
            try
            {
                var bytes = await _pdfDownloader
                    .DownloadAsync(success.Response.PdfDownloadUrl!, cancellationToken)
                    .ConfigureAwait(false);
                pdfBlobRef = await _blob.WriteBinaryAsync(
                    container: "pdf-policies",
                    blobName: $"{ctx.Insurer.Code}/{success.Response.PolicyNumber}.pdf",
                    content: bytes,
                    metadata: new Dictionary<string, string>
                    {
                        ["correlationId"] = ctx.Quotation.CorrelationId,
                        ["insurer"] = ctx.Insurer.Code.ToString(),
                        ["policyNumber"] = success.Response.PolicyNumber,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Descarga fallida no invalida la emisión — la póliza ya existe en la aseguradora.
                // Se puede reintentar la descarga después; queda flag en audit.
                pdfBlobRef = null;
            }
        }

        emission.MarkIssued(success.Response.PolicyNumber, pdfBlobRef, _clock.UtcNow);
        await _emissions.UpdateAsync(emission, cancellationToken).ConfigureAwait(false);

        await _audit.WriteAsync(
            action: "Emission.Issued",
            entityType: "Emission",
            entityId: emission.Id.ToString(),
            payload: new { command.QuotationInsurerResultId, success.Response.PolicyNumber, pdfBlobRef },
            cancellationToken).ConfigureAwait(false);

        return Result<EmitPolicyResult>.Success(
            new EmitPolicyResult(emission.Id, success.Response.PolicyNumber, emission.Status));
    }

    private async Task<Result<EmissionContext>> BuildContextAsync(Guid resultId, CancellationToken ct)
    {
        // Encuentra el QuotationInsurerResult cargando la Quotation y filtrando.
        // (No tenemos un repo dedicado a result; lo hacemos a través del repo de Quotation.)
        Quotation? quotation = null;
        QuotationInsurerResult? result = null;

        // Estrategia simple: el use case actual no necesita listar todas las quotations.
        // Para mantenerlo enfocado, asumimos que el caller pasa un resultId asociado a una Quotation
        // recientemente creada. Implementación temporal: el repo de Quotation carga results al GetByIdAsync.
        // En F5.5 se agregará IQuotationInsurerResultRepository dedicado.
        var quotationId = await TryFindQuotationByResultAsync(resultId, ct).ConfigureAwait(false);
        if (quotationId is null)
        {
            return Result<EmissionContext>.Failure("QuotationInsurerResult not found.");
        }
        quotation = await _quotations.GetByIdAsync(quotationId.Value, ct).ConfigureAwait(false);
        result = quotation?.Results.FirstOrDefault(r => r.Id == resultId);

        if (quotation is null || result is null)
        {
            return Result<EmissionContext>.Failure("QuotationInsurerResult not found.");
        }
        if (result.Status != QuotationInsurerStatus.Succeeded)
        {
            return Result<EmissionContext>.Failure(
                "Only successful quotation results can be emitted.");
        }

        var vehicle = await _vehicles.GetByIdAsync(quotation.VehicleMasterId, ct).ConfigureAwait(false);
        if (vehicle is null) return Result<EmissionContext>.Failure("Vehicle master not found.");

        var insurer = (await _insurers.ListAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(i => i.Id == result.InsurerId);
        if (insurer is null) return Result<EmissionContext>.Failure("Insurer not found.");

        var config = await _configs.GetAsync(insurer.Id, InsurerEnvironment.Production, ct).ConfigureAwait(false)
                  ?? await _configs.GetAsync(insurer.Id, InsurerEnvironment.Staging, ct).ConfigureAwait(false);
        if (config is null) return Result<EmissionContext>.Failure("Insurer configuration not found.");

        var creds = await _credentials.ResolveAsync(config.KeyVaultSecretName, ct).ConfigureAwait(false);

        var adapter = _adapters.FirstOrDefault(a => a.Code == insurer.Code);
        if (adapter is null)
        {
            return Result<EmissionContext>.Failure($"No adapter registered for insurer {insurer.Code}.");
        }

        var mappings = await _mappings.ListByMasterAsync(vehicle.Id, ct).ConfigureAwait(false);
        var insurerMapping = mappings.FirstOrDefault(m => m.InsurerId == insurer.Id);
        if (insurerMapping is null)
        {
            return Result<EmissionContext>.Failure("No vehicle-insurer mapping for this insurer.");
        }

        return Result<EmissionContext>.Success(new EmissionContext(
            quotation, result, vehicle, insurer, config,
            new InsurerCredentialPair_Adapter(creds.Username, creds.Password),
            adapter, insurerMapping.ExternalClave));
    }

    // F5 placeholder: localiza la quotation que contiene el result.
    // En F5.5 se reemplaza por consulta directa via IQuotationInsurerResultRepository.
    private async Task<Guid?> TryFindQuotationByResultAsync(Guid resultId, CancellationToken ct)
    {
        // Necesitamos información del agente actual para acotar la búsqueda.
        var agentId = _currentAgent.AgentId;
        var page = 0;
        const int pageSize = 50;

        while (true)
        {
            var quotations = await _quotations.ListByAgentAsync(agentId, pageSize, page * pageSize, ct).ConfigureAwait(false);
            if (quotations.Count == 0) return null;

            foreach (var q in quotations)
            {
                var hydrated = await _quotations.GetByIdAsync(q.Id, ct).ConfigureAwait(false);
                if (hydrated?.Results.Any(r => r.Id == resultId) == true)
                {
                    return hydrated.Id;
                }
            }

            if (quotations.Count < pageSize) return null;
            page++;
        }
    }

    private static InsurerEmitRequest BuildEmitRequest(EmitPolicyCommand command, EmissionContext ctx)
    {
        var contact = new EmissionContactData(
            command.Customer.FirstName,
            command.Customer.LastNamePaternal,
            command.Customer.LastNameMaternal,
            command.Customer.Rfc,
            command.Customer.Street,
            command.Customer.ExteriorNumber,
            command.Customer.InteriorNumber,
            command.Customer.Neighborhood,
            command.Customer.City,
            command.Customer.StateCode,
            command.Customer.PostalCode,
            command.Customer.Phone,
            command.Customer.Email);

        return new InsurerEmitRequest(
            CorrelationId: ctx.Quotation.CorrelationId,
            Credentials: new InsurerCredentials(ctx.Credentials.Username, ctx.Credentials.Password, ctx.Config.BusinessNumber),
            EnvironmentConfig: new InsurerEnvironmentConfig(ctx.Config.EndpointUrl, ctx.Config.TimeoutSeconds, ctx.Config.MaxRetries),
            ExternalQuoteRef: ctx.Result.ExternalQuoteRef ?? string.Empty,
            Vehicle: new EmissionVehicleData(
                ctx.Vehicle.Year, ctx.Vehicle.Brand, ctx.Vehicle.Model, ctx.Vehicle.Version,
                ctx.ExternalClave,
                command.Customer.Plate,
                command.Customer.EngineNumber,
                command.Customer.SerialNumber),
            Contractor: contact,
            HabitualDriver: contact,
            PremiumTotal: ctx.Result.PremiumTotal ?? 0m,
            PremiumNet: ctx.Result.PremiumNet ?? 0m,
            Tax: ctx.Result.Tax ?? 0m,
            Fees: ctx.Result.Fees ?? 0m);
    }

    private sealed record EmissionContext(
        Quotation Quotation,
        QuotationInsurerResult Result,
        Domain.Catalog.VehicleMaster Vehicle,
        Insurer Insurer,
        InsurerConfig Config,
        InsurerCredentialPair_Adapter Credentials,
        IInsurerAdapter Adapter,
        string ExternalClave);

    private sealed record InsurerCredentialPair_Adapter(string Username, string Password);
}
