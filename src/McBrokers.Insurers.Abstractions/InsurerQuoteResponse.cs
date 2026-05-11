using McBrokers.Domain.Quotations;

namespace McBrokers.Insurers.Abstractions;

public sealed record InsurerQuoteResponse(
    string ExternalQuoteRef,
    decimal PremiumTotal,
    decimal PremiumNet,
    decimal Tax,
    decimal Fees,
    int LatencyMs,
    string RawRequest,
    string RawResponse);

public sealed record InsurerErrorResponse(
    QuotationInsurerStatus Status,
    ErrorCategory Category,
    string ExternalCode,
    string ExternalMessage,
    int LatencyMs,
    string? RawRequest,
    string? RawResponse);
