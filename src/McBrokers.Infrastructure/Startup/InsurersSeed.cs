using McBrokers.Domain.Insurers;
using McBrokers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Startup;

/// <summary>
/// Idempotent seed: asegura que las 5 aseguradoras integradas existen en BD.
/// IsEnabled queda en true por defecto (admin las desactiva si no las quiere visibles).
/// </summary>
public sealed class InsurersSeed : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsurersSeed> _logger;

    private static readonly (InsurerCode Code, string Name, int DisplayOrder)[] DefaultInsurers =
    {
        (InsurerCode.Gnp, "Grupo Nacional Provincial (GNP)", 1),
        (InsurerCode.Qua, "Quálitas", 2),
        (InsurerCode.Ana, "ANA Seguros", 3),
        (InsurerCode.AxaCol, "AXA Colectividad", 4),
        (InsurerCode.AxaDxn, "AXA DXN", 5),
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

            foreach (var (code, name, order) in DefaultInsurers)
            {
                var exists = await db.Insurers.AnyAsync(i => i.Code == code, cancellationToken).ConfigureAwait(false);
                if (exists) continue;

                var creation = Insurer.Create(code, name, order);
                if (!creation.IsSuccess)
                {
                    _logger.LogError("InsurersSeed: {Code} invalid: {Error}", code, creation.Error);
                    continue;
                }

                await db.Insurers.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("InsurersSeed: inserted {Code} ({Name})", code, name);
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
