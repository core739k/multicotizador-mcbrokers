namespace McBrokers.Application.Ports;

public interface IQuotationQueue
{
    ValueTask EnqueueAsync(QuotationWorkItem item, CancellationToken cancellationToken);

    ValueTask<QuotationWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record QuotationWorkItem(Guid QuotationId, string CorrelationId);
