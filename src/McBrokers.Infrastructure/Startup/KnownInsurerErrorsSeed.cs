using McBrokers.Domain.Insurers;
using McBrokers.Domain.Quotations;
using McBrokers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Startup;

/// <summary>
/// Idempotent seed: al arrancar la app, asegura que existen los errores conocidos
/// de las aseguradoras integradas. Sólo INSERTA lo que falta; nunca actualiza ni borra
/// para no pisar ajustes hechos por el equipo de operaciones via admin.
/// </summary>
public sealed class KnownInsurerErrorsSeed : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnownInsurerErrorsSeed> _logger;

    public KnownInsurerErrorsSeed(IServiceScopeFactory scopeFactory, ILogger<KnownInsurerErrorsSeed> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("KnownInsurerErrors seed skipped — database not reachable.");
                return;
            }

            var insurers = await db.Insurers
                .ToDictionaryAsync(i => i.Code, i => i.Id, cancellationToken).ConfigureAwait(false);

            if (insurers.TryGetValue(InsurerCode.Gnp, out var gnpId))
                await SeedGnpErrorsAsync(db, gnpId, cancellationToken).ConfigureAwait(false);

            if (insurers.TryGetValue(InsurerCode.Qua, out var quaId))
                await SeedQualitasErrorsAsync(db, quaId, cancellationToken).ConfigureAwait(false);

            if (insurers.TryGetValue(InsurerCode.Ana, out var anaId))
                await SeedAnaErrorsAsync(db, anaId, cancellationToken).ConfigureAwait(false);

            if (insurers.TryGetValue(InsurerCode.AxaCol, out var axaColId))
                await SeedAxaColErrorsAsync(db, axaColId, cancellationToken).ConfigureAwait(false);

            if (insurers.TryGetValue(InsurerCode.AxaDxn, out var axaDxnId))
                await SeedAxaDxnErrorsAsync(db, axaDxnId, cancellationToken).ConfigureAwait(false);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KnownInsurerErrors seed failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedGnpErrorsAsync(AppDbContext db, Guid insurerId, CancellationToken ct)
    {
        var entries = new[]
        {
            BuildEntry(insurerId, "0288", "vehículo no válido", ErrorCategory.Business,
                "El vehículo no es válido para uso particular en esta versión.",
                "Verifica que la versión del vehículo coincide con la registrada en GNP. Si persiste, intenta cambiar de versión o cotizar uso comercial.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_500", "internal server error", ErrorCategory.InsurerDown,
                "GNP devolvió un error interno (HTTP 500).",
                "Reintentar en unos minutos. Si persiste, notificar a Operaciones.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "HTTP_502", "bad gateway", ErrorCategory.InsurerDown,
                "GNP no está disponible (Bad Gateway).",
                "Reintentar más tarde.",
                AutoRetryStrategy.ExponentialBackoff),
            BuildEntry(insurerId, "HTTP_503", "service unavailable", ErrorCategory.InsurerDown,
                "GNP está fuera de servicio temporalmente.",
                "Reintentar más tarde.",
                AutoRetryStrategy.ExponentialBackoff),
            BuildEntry(insurerId, "TIMEOUT", "timeout", ErrorCategory.InsurerDown,
                "GNP no respondió a tiempo.",
                "Verifica conectividad y reintenta. Si persiste, escalar a Operaciones.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "PARSE_ERROR", "xml malformado", ErrorCategory.Technical,
                "GNP devolvió una respuesta XML que no se pudo interpretar.",
                "Capturar el XML crudo (Blob) y reportar a integraciones GNP.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "MISSING_AMOUNTS", "respuesta incompleta", ErrorCategory.Technical,
                "GNP respondió sin los conceptos económicos esperados (TOTAL_PAGAR / PRIMA_NETA).",
                "Verificar request enviado; podría ser un combo de paquete/cobertura inválido.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "NO_MAPPING", "sin clave amis", ErrorCategory.Business,
                "El vehículo no tiene clave AMIS aprobada para esta aseguradora.",
                "Solicita al admin que apruebe la homologación del vehículo en /Admin/Catalog/Pending o que registre la clave manualmente.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "NO_CONFIG", "sin configuración", ErrorCategory.Technical,
                "La aseguradora no tiene configuración de entorno (endpoint, business number, agente externo).",
                "El admin debe completar /Admin/Insurers/{id}.",
                AutoRetryStrategy.None),
        };

        await InsertMissingAsync(db, "GNP", entries, ct).ConfigureAwait(false);
    }

    private async Task SeedQualitasErrorsAsync(AppDbContext db, Guid insurerId, CancellationToken ct)
    {
        var entries = new[]
        {
            BuildEntry(insurerId, "0288", "uso invalido para esta categoria", ErrorCategory.Business,
                "Quálitas rechazó la cotización porque el uso del vehículo no aplica a esta categoría.",
                "Reintentar cambiando uso a comercial. (El adapter ya hace fallback automático en F4.5).",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "MISSING_PRIMAS", "respuesta sin primas", ErrorCategory.Technical,
                "Quálitas no devolvió el bloque <Primas> esperado.",
                "Verificar paquete/coberturas. Capturar XML crudo y reportar.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "EMPTY_RESULT", "respuesta vacía", ErrorCategory.Technical,
                "Quálitas respondió con Body vacío.",
                "Reintentar; si persiste, escalar a integraciones Quálitas.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "HTTP_500", "internal server error", ErrorCategory.InsurerDown,
                "Quálitas devolvió HTTP 500.",
                "Reintentar en unos minutos.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "TIMEOUT", "timeout", ErrorCategory.InsurerDown,
                "Quálitas no respondió a tiempo.",
                "Reintentar; si persiste, escalar.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "PARSE_ERROR", "xml malformado", ErrorCategory.Technical,
                "Quálitas devolvió XML que no se pudo interpretar.",
                "Capturar XML crudo (Blob) y reportar a integraciones Quálitas.",
                AutoRetryStrategy.None),
        };
        await InsertMissingAsync(db, "Quálitas", entries, ct).ConfigureAwait(false);
    }

    private async Task SeedAnaErrorsAsync(AppDbContext db, Guid insurerId, CancellationToken ct)
    {
        var entries = new[]
        {
            BuildEntry(insurerId, "MISSING_POLIZA", "respuesta sin poliza", ErrorCategory.Technical,
                "ANA no devolvió el bloque <poliza> en la respuesta.",
                "Capturar XML crudo. Si persiste, escalar a integraciones ANA.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "EMPTY_RESULT", "respuesta vacía", ErrorCategory.Technical,
                "ANA respondió con TransaccionResult vacío.",
                "Verificar parámetros Negocio/Usuario/Clave en /Admin/Insurers/{id}.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_500", "internal server error", ErrorCategory.InsurerDown,
                "ANA devolvió HTTP 500.",
                "Reintentar en unos minutos.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "HTTP_503", "service unavailable", ErrorCategory.InsurerDown,
                "ANA está fuera de servicio temporalmente.",
                "Reintentar más tarde.",
                AutoRetryStrategy.ExponentialBackoff),
            BuildEntry(insurerId, "TIMEOUT", "timeout", ErrorCategory.InsurerDown,
                "ANA no respondió a tiempo.",
                "Reintentar; si persiste, escalar a integraciones ANA.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "PARSE_ERROR", "xml malformado", ErrorCategory.Technical,
                "ANA devolvió XML que no se pudo interpretar.",
                "Capturar XML crudo y reportar.",
                AutoRetryStrategy.None),
        };
        await InsertMissingAsync(db, "ANA", entries, ct).ConfigureAwait(false);
    }

    private async Task SeedAxaColErrorsAsync(AppDbContext db, Guid insurerId, CancellationToken ct)
    {
        var entries = new[]
        {
            BuildEntry(insurerId, "EMPTY_RESULT", "respuesta sin return", ErrorCategory.Technical,
                "AXA COL respondió con Body sin <return>.",
                "Verificar credenciales Basic Auth y la SeriePoliza configurada.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "MISSING_AMOUNTS", "sin importes", ErrorCategory.Technical,
                "AXA COL no devolvió PrimaTotal/PrimaNeta.",
                "Revisar coberturas y combinación de paquete enviada.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_401", "unauthorized", ErrorCategory.Technical,
                "AXA COL rechazó las credenciales (HTTP 401).",
                "Verificar Username/Password en Key Vault.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_500", "internal server error", ErrorCategory.InsurerDown,
                "AXA COL devolvió HTTP 500.",
                "Reintentar en unos minutos.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "TIMEOUT", "timeout", ErrorCategory.InsurerDown,
                "AXA COL no respondió en 50s.",
                "Reintentar; si persiste, escalar.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "PARSE_ERROR", "xml malformado", ErrorCategory.Technical,
                "AXA COL devolvió XML que no se pudo interpretar.",
                "Capturar XML crudo y reportar.",
                AutoRetryStrategy.None),
        };
        await InsertMissingAsync(db, "AXA COL", entries, ct).ConfigureAwait(false);
    }

    private async Task SeedAxaDxnErrorsAsync(AppDbContext db, Guid insurerId, CancellationToken ct)
    {
        var entries = new[]
        {
            BuildEntry(insurerId, "MISSING_AMOUNTS", "sin importes", ErrorCategory.Technical,
                "AXA DXN no devolvió primaTotal/primaNeta.",
                "Revisar paquete/coberturas. Capturar XML crudo.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_401", "unauthorized", ErrorCategory.Technical,
                "AXA DXN rechazó las credenciales (HTTP 401).",
                "Verificar Username/Password en Key Vault para AXA DXN.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "HTTP_500", "internal server error", ErrorCategory.InsurerDown,
                "AXA DXN devolvió HTTP 500.",
                "Reintentar en unos minutos.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "TIMEOUT", "timeout", ErrorCategory.InsurerDown,
                "AXA DXN no respondió a tiempo.",
                "Reintentar.",
                AutoRetryStrategy.FixedDelay),
            BuildEntry(insurerId, "PARSE_ERROR", "xml malformado", ErrorCategory.Technical,
                "AXA DXN devolvió XML que no se pudo interpretar.",
                "Capturar XML crudo y reportar.",
                AutoRetryStrategy.None),
            BuildEntry(insurerId, "COPSIS_DOWN", "copsis no disponible", ErrorCategory.InsurerDown,
                "El intermediario COPSIS (emisión AXA DXN) no respondió.",
                "Reintentar emisión; si persiste, escalar a COPSIS.",
                AutoRetryStrategy.ExponentialBackoff),
        };
        await InsertMissingAsync(db, "AXA DXN", entries, ct).ConfigureAwait(false);
    }

    private async Task InsertMissingAsync(
        AppDbContext db, string insurerLabel, IEnumerable<KnownInsurerError> entries, CancellationToken ct)
    {
        foreach (var entry in entries)
        {
            var exists = await db.KnownInsurerErrors
                .AnyAsync(e => e.InsurerId == entry.InsurerId && e.ExternalCode == entry.ExternalCode, ct)
                .ConfigureAwait(false);
            if (!exists)
            {
                await db.KnownInsurerErrors.AddAsync(entry, ct).ConfigureAwait(false);
                _logger.LogInformation("KnownInsurerErrors seed: added {Insurer} error {Code}",
                    insurerLabel, entry.ExternalCode);
            }
        }
    }

    private static KnownInsurerError BuildEntry(
        Guid insurerId, string code, string pattern, ErrorCategory category,
        string humanMessage, string? suggestedAction, AutoRetryStrategy retry)
    {
        var result = KnownInsurerError.Create(insurerId, code, pattern, category, humanMessage, suggestedAction, retry);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"Seed entry '{code}' is invalid: {result.Error}");
    }
}
