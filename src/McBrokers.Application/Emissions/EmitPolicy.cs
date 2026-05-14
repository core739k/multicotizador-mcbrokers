using McBrokers.Application.Blob;
using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Emissions;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.Domain.Quotations;
using McBrokers.Insurers.Abstractions;
using McBrokers.SharedKernel;
using Microsoft.Extensions.Logging;

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
    private readonly IAxaDxnConfigRepository _axaDxnConfigs;
    private readonly IAgentRepository _agents;
    private readonly IBlobStore _blob;
    private readonly IPdfDownloader _pdfDownloader;
    private readonly IEnumerable<IInsurerAdapter> _adapters;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;
    private readonly IKnownInsurerErrorLookup _errorLookup;
    private readonly ILogger<EmitPolicy> _logger;

    public EmitPolicy(
        IEmissionRepository emissions,
        IQuotationRepository quotations,
        IVehicleMasterRepository vehicles,
        IVehicleInsurerMappingRepository mappings,
        IInsurerRepository insurers,
        IInsurerConfigRepository configs,
        IInsurerCredentialProvider credentials,
        IAxaDxnConfigRepository axaDxnConfigs,
        IAgentRepository agents,
        IBlobStore blob,
        IPdfDownloader pdfDownloader,
        IEnumerable<IInsurerAdapter> adapters,
        IAuditWriter audit,
        IClock clock,
        ICurrentAgentProvider currentAgent,
        IKnownInsurerErrorLookup errorLookup,
        ILogger<EmitPolicy> logger)
    {
        _emissions = emissions;
        _quotations = quotations;
        _vehicles = vehicles;
        _mappings = mappings;
        _insurers = insurers;
        _configs = configs;
        _credentials = credentials;
        _axaDxnConfigs = axaDxnConfigs;
        _agents = agents;
        _blob = blob;
        _pdfDownloader = pdfDownloader;
        _adapters = adapters;
        _audit = audit;
        _clock = clock;
        _currentAgent = currentAgent;
        _errorLookup = errorLookup;
        _logger = logger;
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
        if (!context.IsSuccess)
        {
            _logger.LogWarning(
                "EmitPolicy: BuildContext failed for resultId={ResultId}: {Error}",
                command.QuotationInsurerResultId, context.Error);
            return Result<EmitPolicyResult>.Failure(context.Error);
        }

        var ctx = context.Value;

        var emission = existing ?? Emission.Start(
            command.QuotationInsurerResultId, _currentAgent.AgentId, _clock.UtcNow).Value;

        if (existing is null)
        {
            await _emissions.AddAsync(emission, cancellationToken).ConfigureAwait(false);
        }

        // El XML de cotización previa (sin wrapper soapenv) lo necesita el adapter de AXA DXN
        // para embebido en SOLICITUDEMISION/CotizaAutoRespuesta. Otros adapters lo ignoran.
        // Si no podemos leer el blob, seguimos — el builder maneja null con CotizarIncisoResponse vacío.
        var rawQuoteResponseXml = await ReadRawQuoteResponseAsync(ctx.Result, cancellationToken).ConfigureAwait(false);

        // AgentExternalCode → campo <vendedor> del SOLICITUDEMISION. Cargamos el agente para
        // tener su AgentCode externo; null si no lo tiene.
        var agent = await _agents.GetByIdAsync(_currentAgent.AgentId, cancellationToken).ConfigureAwait(false);
        var agentExternalCode = agent?.AgentCode;

        var emitRequest = BuildEmitRequest(command, ctx, rawQuoteResponseXml, agentExternalCode);

        var startedAt = _clock.UtcNow;
        var outcome = await ctx.Adapter.EmitAsync(emitRequest, cancellationToken).ConfigureAwait(false);
        var latency = (int)(_clock.UtcNow - startedAt).TotalMilliseconds;

        // Persistir req/res del adapter de emisión simétrico a
        // ProcessQuotation.PersistBlobsAsync. Sin esto la respuesta de COPSIS
        // se descartaba al terminar el request y no quedaba rastro para auditar
        // rechazos. Path estructurado por año/marca/modelo/correlationId via
        // BlobPaths.Emision; attemptId permite varios intentos sin colisión.
        var (emitReqBlob, emitResBlob) = await PersistEmitBlobsAsync(
            ctx.Quotation.CorrelationId, ctx.Vehicle, ctx.Insurer.Code, outcome, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "EmitPolicy: blobs persistidos correlation={Correlation} insurer={Insurer} req={Req} res={Res}",
            ctx.Quotation.CorrelationId, ctx.Insurer.Code, emitReqBlob, emitResBlob);

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

            _logger.LogWarning(
                "EmitPolicy: adapter {Insurer} returned Failure code={Code} msg={Msg} latency={LatencyMs}ms",
                ctx.Insurer.Code, failure.Error.ExternalCode, humanMessage, failure.Error.LatencyMs);

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
                    path: BlobPaths.PolizaPdf(ctx.Vehicle.Year, ctx.Vehicle.Brand, ctx.Vehicle.Model,
                        ctx.Quotation.CorrelationId, ctx.Insurer.Code),
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

    private async Task<(string? RequestRef, string? ResponseRef)> PersistEmitBlobsAsync(
        string correlationId, Domain.Catalog.VehicleMaster vehicle, InsurerCode insurerCode,
        InsurerEmitOutcome outcome, CancellationToken ct)
    {
        var (requestBody, responseBody) = outcome switch
        {
            InsurerEmitOutcome.Success s => (s.Response.RawRequest, s.Response.RawResponse),
            InsurerEmitOutcome.Failure f => (f.Error.RawRequest ?? string.Empty, f.Error.RawResponse ?? string.Empty),
            _ => (string.Empty, string.Empty),
        };

        // Si el adapter no capturó body (caso defensivo, ej. timeout antes de
        // recibir respuesta), no escribimos blob vacío.
        if (string.IsNullOrWhiteSpace(requestBody) && string.IsNullOrWhiteSpace(responseBody))
        {
            return (null, null);
        }

        var attemptId = Guid.NewGuid().ToString("n");
        var metadata = new Dictionary<string, string>
        {
            ["correlationId"] = correlationId,
            ["insurer"] = insurerCode.ToString(),
            ["operation"] = "emision",
        };

        string? reqRef = null;
        string? resRef = null;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            reqRef = await _blob.WriteAsync(
                path: BlobPaths.Emision(vehicle.Year, vehicle.Brand, vehicle.Model,
                    correlationId, insurerCode, attemptId, BlobRole.Request),
                content: requestBody,
                metadata: metadata,
                cancellationToken: ct).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            resRef = await _blob.WriteAsync(
                path: BlobPaths.Emision(vehicle.Year, vehicle.Brand, vehicle.Model,
                    correlationId, insurerCode, attemptId, BlobRole.Response),
                content: responseBody,
                metadata: metadata,
                cancellationToken: ct).ConfigureAwait(false);
        }
        return (reqRef, resRef);
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

        var config = await _configs.GetAsync(insurer.Id, ct).ConfigureAwait(false);
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

        // POC AXA DXN: construir el typed business config para que la emisión vía COPSIS
        // tenga acceso a póliza/tarifa/mes. Otras aseguradoras no usan BusinessConfig todavía.
        InsurerBusinessConfig? businessConfig = null;
        if (insurer.Code == InsurerCode.AxaDxn)
        {
            var snapshot = await _axaDxnConfigs.GetByInsurerIdAsync(insurer.Id, ct).ConfigureAwait(false);
            if (snapshot is not null)
            {
                var business = snapshot.Businesses.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.PolizaAutos))
                            ?? snapshot.Businesses.FirstOrDefault();
                businessConfig = new AxaDxnAdapterConfig(
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
        }

        return Result<EmissionContext>.Success(new EmissionContext(
            quotation, result, vehicle, insurer, config,
            new InsurerCredentialPair_Adapter(creds.Username, creds.Password),
            adapter, insurerMapping.ExternalClave, businessConfig));
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

    private async Task<string?> ReadRawQuoteResponseAsync(
        QuotationInsurerResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.ResponseBlobRef)) return null;
        try
        {
            return await _blob.ReadAsync(result.ResponseBlobRef, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Si el blob no es accesible no abortamos la emisión; el adapter sigue con
            // CotizarIncisoResponse vacío y COPSIS probablemente rechazará — pero al menos
            // queda el rastro del intento.
            _logger.LogWarning(ex, "EmitPolicy: no se pudo leer blob de cotización {Ref}", result.ResponseBlobRef);
            return null;
        }
    }

    private static InsurerEmitRequest BuildEmitRequest(
        EmitPolicyCommand command, EmissionContext ctx,
        string? rawQuoteResponseXml, string? agentExternalCode)
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

        // Valuation efectiva: override de la card si existe, sino el valor base de la Quotation.
        var valuation = ctx.Result.Overrides?.Valuation ?? ctx.Quotation.ValuationType;

        return new InsurerEmitRequest(
            CorrelationId: ctx.Quotation.CorrelationId,
            Credentials: new InsurerCredentials(ctx.Credentials.Username, ctx.Credentials.Password, ctx.Config.BusinessNumber),
            Connection: new InsurerConnectionConfig(ctx.Config.EndpointUrl, ctx.Config.TimeoutSeconds, ctx.Config.MaxRetries),
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
            Fees: ctx.Result.Fees ?? 0m,
            Valuation: valuation,
            RawQuoteResponseXml: rawQuoteResponseXml,
            AgentExternalCode: agentExternalCode,
            BusinessConfig: ctx.BusinessConfig);
    }

    private sealed record EmissionContext(
        Quotation Quotation,
        QuotationInsurerResult Result,
        Domain.Catalog.VehicleMaster Vehicle,
        Insurer Insurer,
        InsurerConfig Config,
        InsurerCredentialPair_Adapter Credentials,
        IInsurerAdapter Adapter,
        string ExternalClave,
        InsurerBusinessConfig? BusinessConfig);

    private sealed record InsurerCredentialPair_Adapter(string Username, string Password);
}
