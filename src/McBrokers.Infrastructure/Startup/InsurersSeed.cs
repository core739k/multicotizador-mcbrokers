using McBrokers.Domain.Insurers;
using McBrokers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Startup;

/// <summary>
/// Idempotent seed: asegura que las 5 aseguradoras integradas existen en BD.
/// AXA COL nace deshabilitada — MCBrokers solo opera AXA DXN. El resto nace habilitada
/// (el admin puede activar/desactivar después).
/// </summary>
public sealed class InsurersSeed : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsurersSeed> _logger;

    private static readonly (InsurerCode Code, string Name, int DisplayOrder, bool IsEnabled)[] DefaultInsurers =
    {
        (InsurerCode.Gnp, "Grupo Nacional Provincial (GNP)", 1, true),
        (InsurerCode.Qua, "Quálitas", 2, true),
        (InsurerCode.Ana, "ANA Seguros", 3, true),
        (InsurerCode.AxaCol, "AXA Colectividad", 4, false),
        (InsurerCode.AxaDxn, "AXA DXN", 5, true),
    };

    public InsurersSeed(IServiceScopeFactory scopeFactory, ILogger<InsurersSeed> logger)
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
                _logger.LogWarning("InsurersSeed skipped — database not reachable.");
                return;
            }

            foreach (var (code, name, order, isEnabled) in DefaultInsurers)
            {
                var exists = await db.Insurers.AnyAsync(i => i.Code == code, cancellationToken).ConfigureAwait(false);
                if (exists) continue;

                var creation = Insurer.Create(code, name, order);
                if (!creation.IsSuccess)
                {
                    _logger.LogError("InsurersSeed: {Code} invalid: {Error}", code, creation.Error);
                    continue;
                }

                if (!isEnabled)
                {
                    creation.Value.Disable();
                }

                await db.Insurers.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("InsurersSeed: inserted {Code} ({Name}) IsEnabled={IsEnabled}", code, name, isEnabled);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsurersSeed failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
