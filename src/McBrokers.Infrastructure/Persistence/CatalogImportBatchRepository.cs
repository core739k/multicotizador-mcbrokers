using McBrokers.Application.Ports;
using McBrokers.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class CatalogImportBatchRepository : ICatalogImportBatchRepository
{
    private readonly AppDbContext _db;
    public CatalogImportBatchRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CatalogImportBatch>> ListRecentAsync(int take, CancellationToken cancellationToken) =>
        await _db.CatalogImportBatches
            .OrderByDescending(b => b.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(CatalogImportBatch batch, CancellationToken cancellationToken)
    {
        await _db.CatalogImportBatches.AddAsync(batch, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(CatalogImportBatch batch, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
