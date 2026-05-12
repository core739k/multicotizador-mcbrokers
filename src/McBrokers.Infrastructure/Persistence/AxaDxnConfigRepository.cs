using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers.AxaDxn;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public sealed class AxaDxnConfigRepository : IAxaDxnConfigRepository
{
    private readonly AppDbContext _db;

    public AxaDxnConfigRepository(AppDbContext db) => _db = db;

    public async Task<AxaDxnConfigWithBusinesses?> GetByInsurerIdAsync(
        Guid insurerId, CancellationToken cancellationToken)
    {
        var config = await _db.AxaDxnConfigs
            .SingleOrDefaultAsync(c => c.InsurerId == insurerId, cancellationToken)
            .ConfigureAwait(false);
        if (config is null) return null;

        var businesses = await _db.AxaDxnBusinesses
            .Where(b => b.AxaDxnConfigId == config.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AxaDxnConfigWithBusinesses(config, businesses);
    }

    public async Task AddAsync(AxaDxnConfig config, CancellationToken cancellationToken)
    {
        await _db.AxaDxnConfigs.AddAsync(config, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AxaDxnConfig config, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public Task<AxaDxnBusiness?> GetBusinessAsync(
        Guid axaDxnConfigId, AxaDxnBusinessName nombre, CancellationToken cancellationToken) =>
        _db.AxaDxnBusinesses.SingleOrDefaultAsync(
            b => b.AxaDxnConfigId == axaDxnConfigId && b.Nombre == nombre,
            cancellationToken);

    public async Task AddBusinessAsync(AxaDxnBusiness business, CancellationToken cancellationToken)
    {
        await _db.AxaDxnBusinesses.AddAsync(business, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateBusinessAsync(AxaDxnBusiness business, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
