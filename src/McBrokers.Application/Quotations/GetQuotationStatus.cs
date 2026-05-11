using McBrokers.Application.Ports;
using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Quotations;

public sealed record QuotationStatusView(
    Guid Id,
    string CorrelationId,
    QuotationStatus Status,
    int ExpectedResultsCount,
    IReadOnlyList<QuotationResultView> Results);

public sealed record QuotationResultView(
    Guid Id,
    Guid InsurerId,
    QuotationInsurerStatus Status,
    ErrorCategory ErrorCategory,
    string? ErrorCode,
    string? ErrorMessage,
    decimal? PremiumTotal,
    decimal? PremiumNet,
    decimal? Tax,
    decimal? Fees,
    int LatencyMs,
    string? ExternalQuoteRef);

public sealed class GetQuotationStatus
{
    private readonly IQuotationRepository _quotations;
    public GetQuotationStatus(IQuotationRepository quotations) => _quotations = quotations;

    public async Task<QuotationStatusView?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var q = await _quotations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (q is null) return null;

        return new QuotationStatusView(
            q.Id, q.CorrelationId, q.Status, q.ExpectedResultsCount,
            q.Results.Select(r => new QuotationResultView(
                r.Id, r.InsurerId, r.Status, r.ErrorCategory,
                r.ErrorCode, r.ErrorMessageHuman,
                r.PremiumTotal, r.PremiumNet, r.Tax, r.Fees,
                r.LatencyMs, r.ExternalQuoteRef)).ToList());
    }
}
