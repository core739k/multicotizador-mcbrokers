using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.SharedKernel;

namespace McBrokers.Application.Catalog.Importers;

/// <summary>
/// Orquestador del importador del catálogo de vehículos AXA DXN.
/// Dispara 4 llamadas SOAP (Marca + Submarca por cada tarifa configurada),
/// filtra idTipoVehiculo excluidos {22, 3, 81, 7, 24}, expande filas por
/// año dentro de [currentYear-1, currentYear] y delega la persistencia a
/// IImportInsurerCatalog con IsSourceOfTruth=true.
/// </summary>
public sealed class RunAxaDxnCatalogImport
{
    private const string FallbackEndpoint =
        "https://serviciosweb.axa.com.mx:9104/EmisionPolizasWS/services/SolicitudPolizasService";

    private const string ImportSource = "AxaDxn-WS";

    private static readonly HashSet<string> ExcludedIdTipoVehiculo =
        new(StringComparer.Ordinal) { "22", "3", "81", "7", "24" };

    private readonly IInsurerRepository _insurers;
    private readonly IAxaDxnConfigRepository _axaConfigs;
    private readonly IInsurerConfigRepository _insurerConfigs;
    private readonly IAxaDxnCatalogClient _client;
    private readonly IClock _clock;
    private readonly IImportInsurerCatalog _importer;

    public RunAxaDxnCatalogImport(
        IInsurerRepository insurers,
        IAxaDxnConfigRepository axaConfigs,
        IInsurerConfigRepository insurerConfigs,
        IAxaDxnCatalogClient client,
        IClock clock,
        IImportInsurerCatalog importer)
    {
        _insurers = insurers;
        _axaConfigs = axaConfigs;
        _insurerConfigs = insurerConfigs;
        _client = client;
        _clock = clock;
        _importer = importer;
    }

    public async Task<Result<RunAxaDxnCatalogImportResult>> ExecuteAsync(
        Guid insurerId, CancellationToken cancellationToken)
    {
        var insurer = await _insurers.GetByIdAsync(insurerId, cancellationToken).ConfigureAwait(false);
        if (insurer is null)
        {
            return Result<RunAxaDxnCatalogImportResult>.Failure("INSURER_NOT_FOUND");
        }

        if (insurer.Code != InsurerCode.AxaDxn)
        {
            return Result<RunAxaDxnCatalogImportResult>.Failure("INVALID_INSURER_FOR_AXA_DXN");
        }

        var configWB = await _axaConfigs.GetByInsurerIdAsync(insurerId, cancellationToken).ConfigureAwait(false);
        if (configWB is null)
        {
            return Result<RunAxaDxnCatalogImportResult>.Failure("AXA_DXN_CONFIG_MISSING");
        }

        var insurerCfg = await _insurerConfigs.GetAsync(insurerId, cancellationToken).ConfigureAwait(false);
        var endpoint = insurerCfg?.EndpointUrl ?? FallbackEndpoint;

        var credentials = new AxaDxnCatalogCredentials(
            configWB.Config.Usuario, configWB.Config.Password, endpoint);

        var currentYear = _clock.UtcNow.Year;
        var allRows = new List<CatalogImportRow>();
        var tarifas = new[] { configWB.Config.Tarifa, configWB.Config.TarifaPickup };

        foreach (var tarifa in tarifas)
        {
            var marcas = await _client.FetchAsync(credentials, tarifa, "Marca", cancellationToken).ConfigureAwait(false);
            if (!marcas.IsSuccess)
            {
                return Result<RunAxaDxnCatalogImportResult>.Failure(
                    $"AXA_DXN_FETCH_FAILED: Tarifa={tarifa}, Catalogo=Marca - {marcas.Error}");
            }

            var submarcas = await _client.FetchAsync(credentials, tarifa, "Submarca", cancellationToken).ConfigureAwait(false);
            if (!submarcas.IsSuccess)
            {
                return Result<RunAxaDxnCatalogImportResult>.Failure(
                    $"AXA_DXN_FETCH_FAILED: Tarifa={tarifa}, Catalogo=Submarca - {submarcas.Error}");
            }

            allRows.AddRange(MapToRows(marcas.Value, submarcas.Value, currentYear));
        }

        var importResult = await _importer.ExecuteAsync(
            new ImportInsurerCatalogCommand(insurerId, ImportSource, IsSourceOfTruth: true, allRows),
            cancellationToken).ConfigureAwait(false);

        if (!importResult.IsSuccess)
        {
            return Result<RunAxaDxnCatalogImportResult>.Failure(importResult.Error);
        }

        return Result<RunAxaDxnCatalogImportResult>.Success(new RunAxaDxnCatalogImportResult(
            importResult.Value.BatchId,
            importResult.Value.Total,
            importResult.Value.AutoApproved,
            importResult.Value.PendingReview,
            importResult.Value.Rejected));
    }

    private static IEnumerable<CatalogImportRow> MapToRows(
        IReadOnlyList<AxaDxnCatalogRecord> marcas,
        IReadOnlyList<AxaDxnCatalogRecord> submarcas,
        int currentYear)
    {
        var brandByIdMarca = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in marcas)
        {
            if (m.IdMarca is null || m.Descripcion is null) continue;
            if (m.IdTipoVehiculo is not null && ExcludedIdTipoVehiculo.Contains(m.IdTipoVehiculo)) continue;
            brandByIdMarca.TryAdd(m.IdMarca, m.Descripcion);
        }

        var yearMin = currentYear - 1;
        var yearMax = currentYear;

        foreach (var s in submarcas)
        {
            if (s.IdMarca is null || s.Descripcion is null || s.ClaveAmis is null) continue;
            if (s.ModeloDesde is null || s.ModeloHasta is null) continue;
            if (s.IdTipoVehiculo is not null && ExcludedIdTipoVehiculo.Contains(s.IdTipoVehiculo)) continue;
            if (!brandByIdMarca.TryGetValue(s.IdMarca, out var brand)) continue;

            var fromYear = Math.Max(s.ModeloDesde.Value, yearMin);
            var toYear = Math.Min(s.ModeloHasta.Value, yearMax);
            if (fromYear > toYear) continue;

            var model = FirstWord(s.Descripcion);
            var version = s.Descripcion;

            for (var year = fromYear; year <= toYear; year++)
            {
                // ExternalClave compuesta {claveAMIS}|{year}: el unique index
                // UX_VehicleInsurerMappings_Insurer_ExternalClave fuerza unicidad por
                // (insurer, externalClave). Como una misma claveAMIS aplica a varios
                // años y necesitamos un VehicleMaster por año, codificamos el año dentro
                // de la clave. El adapter de cotización/emisión debe parsear '|' para
                // recuperar la claveAMIS pura.
                // TODO(refresh-mode): cuando el modelo de idempotencia evolucione a
                // (insurer, externalClave, year), esta composición puede colapsar a
                // claveAMIS pura.
                var externalClave = $"{s.ClaveAmis}|{year}";

                yield return new CatalogImportRow(
                    Year: year,
                    Brand: brand,
                    Model: model,
                    Version: version,
                    ExternalClave: externalClave,
                    BodyType: null,
                    Transmission: null);
            }
        }
    }

    private static string FirstWord(string descripcion)
    {
        var first = descripcion.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? descripcion : first;
    }
}
