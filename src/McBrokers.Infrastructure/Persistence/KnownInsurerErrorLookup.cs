using McBrokers.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace McBrokers.Infrastructure.Persistence;

/// <summary>
/// Lookup con cache 5 minutos por (insurerId, externalCode). Ante un cambio admin,
/// el TTL garantiza convergencia en máximo 5 min sin necesidad de invalidar manualmente.
/// </summary>
public sealed class KnownInsurerErrorLookup : IKnownInsurerErrorLookup
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public KnownInsurerErrorLookup(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<KnownInsurerErrorView?> FindAsync(
        Guid insurerId, string externalCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalCode)) return null;

        var key = $"kie:{insurerId}:{externalCode}";
        if (_cache.TryGetValue<KnownInsurerErrorView?>(key, out var cached))
        {
            return cached;
        }

        var error = await _db.KnownInsurerErrors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.InsurerId == insurerId && e.ExternalCode == externalCode,
                cancellationToken)
            .ConfigureAwait(false);

        var view = error is null
            ? null
            : new KnownInsurerErrorView(
                error.ExternalCode, error.Category, error.HumanMessage,
                error.SuggestedAction, error.AutoRetry);

        _cache.Set(key, view, CacheTtl);
        return view;
    }
}
