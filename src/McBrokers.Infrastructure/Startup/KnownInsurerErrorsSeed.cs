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

            var gnp = await db.Insurers.SingleOrDefaultAsync(
                i => i.Code == InsurerCode.Gnp, cancellationToken).ConfigureAwait(false);

            if (gnp is null)
            {
                _logger.LogInformation("KnownInsurerErrors seed: GNP not registered yet, skipping.");
                return;
            }

            await SeedGnpErrorsAsync(db, gnp.Id, cancellationToken).ConfigureAwait(false);
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

        foreach (var entry in entries)
        {
            var exists = await db.KnownInsurerErrors
                .AnyAsync(e => e.InsurerId == insurerId && e.ExternalCode == entry.ExternalCode, ct)
                .ConfigureAwait(false);
            if (!exists)
            {
                await db.KnownInsurerErrors.AddAsync(entry, ct).ConfigureAwait(false);
                _logger.LogInformation("KnownInsurerErrors seed: added GNP error {Code}", entry.ExternalCode);
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
                $"Seed entry for GNP error '{code}' is invalid: {result.Error}");
    }
}
