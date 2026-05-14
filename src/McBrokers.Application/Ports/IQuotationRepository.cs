using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Ports;

public interface IQuotationRepository
{
    Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Quotation?> FindByResultIdAsync(Guid resultId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Quotation>> ListByAgentAsync(Guid agentId, int take, int skip, CancellationToken cancellationToken);

    Task AddAsync(Quotation quotation, CancellationToken cancellationToken);

    Task UpdateAsync(Quotation quotation, CancellationToken cancellationToken);

    Task AppendResultAsync(QuotationInsurerResult result, CancellationToken cancellationToken);
}
