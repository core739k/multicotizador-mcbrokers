using McBrokers.Application.Ports;
using McBrokers.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace McBrokers.Infrastructure.Persistence;

public class QuotationRepository : IQuotationRepository
{
    private readonly AppDbContext _db;
    public QuotationRepository(AppDbContext db) => _db = db;

    public async Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var quotation = await _db.Quotations.SingleOrDefaultAsync(q => q.Id == id, cancellationToken).ConfigureAwait(false);
        if (quotation is null) return null;

        var results = await _db.QuotationInsurerResults
            .Where(r => r.QuotationId == id)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        quotation.Rehydrate(results);
        return quotation;
    }

    public async Task<Quotation?> FindByResultIdAsync(Guid resultId, CancellationToken cancellationToken)
    {
        var quotationId = await _db.QuotationInsurerResults
            .Where(r => r.Id == resultId)
            .Select(r => (Guid?)r.QuotationId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (quotationId is null) return null;
        return await GetByIdAsync(quotationId.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Quotation>> ListByAgentAsync(Guid agentId, int take, int skip, CancellationToken cancellationToken) =>
        await _db.Quotations
            .Where(q => q.AgentId == agentId)
            .OrderByDescending(q => q.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Quotation>> ListRecentAsync(int take, int skip, CancellationToken cancellationToken) =>
        await _db.Quotations
            .OrderByDescending(q => q.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        _db.Quotations.CountAsync(cancellationToken);

    public async Task AddAsync(Quotation quotation, CancellationToken cancellationToken)
    {
        await _db.Quotations.AddAsync(quotation, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Quotation quotation, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task AppendResultAsync(QuotationInsurerResult result, CancellationToken cancellationToken)
    {
        await _db.QuotationInsurerResults.AddAsync(result, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
