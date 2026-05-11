using McBrokers.Domain.Quotations;

namespace McBrokers.Insurers.Abstractions;

public abstract record InsurerEmitOutcome
{
    public sealed record Success(InsurerEmitResponse Response) : InsurerEmitOutcome;
    public sealed record Failure(InsurerEmitError Error) : InsurerEmitOutcome;
}

public sealed record InsurerEmitResponse(
    string PolicyNumber,
    string? PdfDownloadUrl,
    int LatencyMs,
    string RawRequest,
    string RawResponse);

public sealed record InsurerEmitError(
    ErrorCategory Category,
    string ExternalCode,
    string ExternalMessage,
    int LatencyMs,
    string? RawRequest,
    string? RawResponse);
