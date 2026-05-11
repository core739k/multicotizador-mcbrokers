using McBrokers.Application.Ports;
using McBrokers.Domain.Emissions;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class EmissionRepository : IEmissionRepository
{
    private readonly AppDbContext _db;
    public EmissionRepository(AppDbContext db) => _db = db;

    public Task<Emission?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Emissions.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Emission?> GetByQuotationResultAsync(Guid quotationInsurerResultId, CancellationToken cancellationToken) =>
        _db.Emissions.SingleOrDefaultAsync(e => e.QuotationInsurerResultId == quotationInsurerResultId, cancellationToken);

    public async Task AddAsync(Emission emission, CancellationToken cancellationToken)
    {
        await _db.Emissions.AddAsync(emission, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Emission emission, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task AddAttemptAsync(EmissionAttempt attempt, CancellationToken cancellationToken)
    {
        await _db.EmissionAttempts.AddAsync(attempt, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
