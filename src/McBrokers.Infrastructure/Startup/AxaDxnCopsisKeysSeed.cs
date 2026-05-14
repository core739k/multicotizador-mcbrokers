using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using McBrokers.Domain.Insurers.AxaDxn;
using McBrokers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Startup;

/// <summary>
/// Idempotent seed: actualiza CopsisD4Key y CopsisB con los valores reales del legacy si
/// están en "pending". Las llaves son constantes del gateway COPSIS (no per-corredor) y
/// el legacy las tenía hardcoded (CotizacionNegocio.cs:5287-5289). Las dejamos en BD para
/// no romper el modelo, pero un seeder se asegura de que arranquen con valores válidos.
/// </summary>
public sealed class AxaDxnCopsisKeysSeed : IHostedService
{
    // Llaves reales del gateway COPSIS (hardcoded en el legacy, integradas con AXA al onboarding).
    // No son secretos rotables per-corredor; ya estaban en el código legacy.
    internal const string RealCopsisD4Key =
        "uLmdvb2dsZS5jb20vY2hhdC1kYXRhYmFzZS05NTdjMSIsIm5hbWUiOiJIZWJlc";
    internal const string RealCopsisB =
        "r7etUUfQCT4w0mwXtrEjbBPCqD2n+Ce9xw3LOnfk6mw=";

    // Token sentinel del seed inicial. Sólo actualizamos si los valores actuales son este
    // string — así no pisamos ediciones manuales hechas desde el admin.
    private const string PendingPlaceholder = "pending";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AxaDxnCopsisKeysSeed> _logger;

    public AxaDxnCopsisKeysSeed(
        IServiceScopeFactory scopeFactory, ILogger<AxaDxnCopsisKeysSeed> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AxaDxnCopsisKeysSeed starting…");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("AxaDxnCopsisKeysSeed skipped — database not reachable.");
                return;
            }

            var axaDxn = await db.Insurers
                .SingleOrDefaultAsync(i => i.Code == InsurerCode.AxaDxn, cancellationToken)
                .ConfigureAwait(false);
            if (axaDxn is null)
            {
                _logger.LogInformation("AxaDxnCopsisKeysSeed skipped — AxaDxn insurer not seeded yet.");
                return;
            }

            // El value converter descifra al materializar — leemos plaintext.
            var config = await db.AxaDxnConfigs
                .SingleOrDefaultAsync(c => c.InsurerId == axaDxn.Id, cancellationToken)
                .ConfigureAwait(false);
            if (config is null)
            {
                _logger.LogInformation("AxaDxnCopsisKeysSeed skipped — no AxaDxnConfig yet.");
                return;
            }

            var needsKey = string.Equals(config.CopsisD4Key, PendingPlaceholder, StringComparison.Ordinal);
            var needsB = string.Equals(config.CopsisB, PendingPlaceholder, StringComparison.Ordinal);
            if (!needsKey && !needsB)
            {
                _logger.LogDebug("AxaDxnCopsisKeysSeed: claves COPSIS ya configuradas (no se tocan).");
                return;
            }

            // El método Update del aggregate exige todos los campos — replicamos los existentes
            // y solo sobrescribimos las llaves pendientes.
            var result = config.Update(
                usuario: config.Usuario,
                password: config.Password,
                tarifa: config.Tarifa,
                tarifaPickup: config.TarifaPickup,
                descuento: config.Descuento,
                descuentoPickup: config.DescuentoPickup,
                mesPolizaDefault: config.MesPolizaDefault,
                copsisD4Key: needsKey ? RealCopsisD4Key : config.CopsisD4Key,
                copsisB: needsB ? RealCopsisB : config.CopsisB);

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "AxaDxnCopsisKeysSeed: AxaDxnConfig.Update inválido: {Error}", result.Error);
                return;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "AxaDxnCopsisKeysSeed: AxaDxnConfig actualizado con llaves COPSIS reales (D4Key={UpdK}, B={UpdB}).",
                needsKey, needsB);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AxaDxnCopsisKeysSeed failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
