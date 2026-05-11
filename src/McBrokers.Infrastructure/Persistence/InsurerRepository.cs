using McBrokers.Application.Ports;
using McBrokers.Domain.Insurers;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class InsurerRepository : IInsurerRepository
{
    private readonly AppDbContext _db;

    public InsurerRepository(AppDbContext db) => _db = db;

    public Task<Insurer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Insurers.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Insurer?> GetByCodeAsync(InsurerCode code, CancellationToken cancellationToken) =>
        _db.Insurers.SingleOrDefaultAsync(i => i.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Insurer>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Insurers.ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(Insurer insurer, CancellationToken cancellationToken)
    {
        await _db.Insurers.AddAsync(insurer, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Insurer insurer, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
