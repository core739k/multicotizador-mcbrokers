using McBrokers.SharedKernel;

namespace McBrokers.Domain.Emissions;

public sealed class EmissionAttempt
{
    public Guid Id { get; }
    public Guid EmissionId { get; }
    public int AttemptNumber { get; }
    public string Outcome { get; }
    public int LatencyMs { get; }
    public string? ErrorCode { get; }
    public DateTime CreatedAt { get; }

    private EmissionAttempt(
        Guid id, Guid emissionId, int attemptNumber, string outcome,
        int latencyMs, string? errorCode, DateTime createdAt)
    {
        Id = id;
        EmissionId = emissionId;
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        LatencyMs = latencyMs;
        ErrorCode = errorCode;
        CreatedAt = createdAt;
    }

    public static Result<EmissionAttempt> Create(
        Guid emissionId, int attemptNumber, string outcome,
        int latencyMs, string? errorCode, DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<EmissionAttempt>.Failure("createdAt must be UTC.");
        }
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return Result<EmissionAttempt>.Failure("Outcome must not be empty.");
        }
        if (attemptNumber < 1)
        {
            return Result<EmissionAttempt>.Failure("AttemptNumber must be >= 1.");
        }

        return Result<EmissionAttempt>.Success(new EmissionAttempt(
            Guid.NewGuid(), emissionId, attemptNumber, outcome.Trim(),
            latencyMs, errorCode, createdAt));
    }
}
