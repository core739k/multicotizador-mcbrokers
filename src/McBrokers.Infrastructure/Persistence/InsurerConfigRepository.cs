using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class InsurerConfigRepository : IInsurerConfigRepository
{
    private readonly AppDbContext _db;

    public InsurerConfigRepository(AppDbContext db) => _db = db;

    public Task<InsurerConfig?> GetAsync(Guid insurerId, InsurerEnvironment environment, CancellationToken cancellationToken) =>
        _db.InsurerConfigs.SingleOrDefaultAsync(
            c => c.InsurerId == insurerId && c.Environment == environment,
            cancellationToken);

    public async Task<IReadOnlyList<InsurerConfig>> ListByInsurerAsync(Guid insurerId, CancellationToken cancellationToken) =>
        await _db.InsurerConfigs
            .Where(c => c.InsurerId == insurerId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(InsurerConfig config, CancellationToken cancellationToken)
    {
        await _db.InsurerConfigs.AddAsync(config, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(InsurerConfig config, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
