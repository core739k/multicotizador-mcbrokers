using McBrokers.SharedKernel;

namespace McBrokers.Domain.Quotations;

public sealed class KnownInsurerError
{
    public Guid Id { get; }
    public Guid InsurerId { get; }
    public string ExternalCode { get; }
    public string ExternalMessagePattern { get; }
    public ErrorCategory Category { get; }
    public string HumanMessage { get; }
    public string? SuggestedAction { get; }
    public AutoRetryStrategy AutoRetry { get; }

    private KnownInsurerError(
        Guid id, Guid insurerId, string externalCode, string externalMessagePattern,
        ErrorCategory category, string humanMessage, string? suggestedAction,
        AutoRetryStrategy autoRetry)
    {
        Id = id;
        InsurerId = insurerId;
        ExternalCode = externalCode;
        ExternalMessagePattern = externalMessagePattern;
        Category = category;
        HumanMessage = humanMessage;
        SuggestedAction = suggestedAction;
        AutoRetry = autoRetry;
    }

    public static Result<KnownInsurerError> Create(
        Guid insurerId, string externalCode, string externalMessagePattern,
        ErrorCategory category, string humanMessage, string? suggestedAction,
        AutoRetryStrategy autoRetry)
    {
        if (string.IsNullOrWhiteSpace(externalCode))
        {
            return Result<KnownInsurerError>.Failure("ExternalCode must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(humanMessage))
        {
            return Result<KnownInsurerError>.Failure("HumanMessage must not be empty.");
        }

        return Result<KnownInsurerError>.Success(new KnownInsurerError(
            Guid.NewGuid(), insurerId,
            externalCode.Trim(), externalMessagePattern ?? string.Empty,
            category, humanMessage.Trim(), suggestedAction?.Trim(),
            autoRetry));
    }
}

public enum AutoRetryStrategy
{
    None = 0,
    FixedDelay = 1,
    ExponentialBackoff = 2,
}
