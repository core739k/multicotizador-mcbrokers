using McBrokers.Domain.Insurers;

namespace McBrokers.Insurers.Abstractions;

public interface IInsurerAdapter
{
    InsurerCode Code { get; }

    Task<InsurerQuoteOutcome> QuoteAsync(
        InsurerQuoteRequest request,
        CancellationToken cancellationToken);
}
