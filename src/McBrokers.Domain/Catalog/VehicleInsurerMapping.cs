using McBrokers.SharedKernel;

namespace McBrokers.Domain.Catalog;

public sealed class VehicleInsurerMapping
{
    public const decimal AutoApprovalThreshold = 95m;

    public Guid Id { get; }
    public Guid VehicleMasterId { get; }
    public Guid InsurerId { get; }
    public string ExternalClave { get; }
    public string InsurerBrandRaw { get; }
    public string InsurerModelRaw { get; }
    public string InsurerVersionRaw { get; }
    public decimal ConfidenceScore { get; }
    public ReviewState ReviewState { get; private set; }
    public Guid? ReviewedByAgentId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public DateTime CreatedAt { get; }

    private VehicleInsurerMapping(
        Guid id, Guid vehicleMasterId, Guid insurerId,
        string externalClave, string insurerBrandRaw, string insurerModelRaw, string insurerVersionRaw,
        decimal confidenceScore, ReviewState reviewState, DateTime createdAt)
    {
        Id = id;
        VehicleMasterId = vehicleMasterId;
        InsurerId = insurerId;
        ExternalClave = externalClave;
        InsurerBrandRaw = insurerBrandRaw;
        InsurerModelRaw = insurerModelRaw;
        InsurerVersionRaw = insurerVersionRaw;
        ConfidenceScore = confidenceScore;
        ReviewState = reviewState;
        CreatedAt = createdAt;
    }

    public static Result<VehicleInsurerMapping> Create(
        Guid vehicleMasterId, Guid insurerId,
        string externalClave, string insurerBrandRaw, string insurerModelRaw, string insurerVersionRaw,
        decimal confidenceScore, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(externalClave))
        {
            return Result<VehicleInsurerMapping>.Failure("ExternalClave must not be empty.");
        }

        if (confidenceScore < 0m || confidenceScore > 100m)
        {
            return Result<VehicleInsurerMapping>.Failure("ConfidenceScore must be between 0 and 100.");
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<VehicleInsurerMapping>.Failure("createdAt must be UTC.");
        }

        var state = confidenceScore >= AutoApprovalThreshold ? ReviewState.Approved : ReviewState.Pending;

        return Result<VehicleInsurerMapping>.Success(new VehicleInsurerMapping(
            Guid.NewGuid(),
            vehicleMasterId,
            insurerId,
            externalClave.Trim(),
            (insurerBrandRaw ?? string.Empty).Trim(),
            (insurerModelRaw ?? string.Empty).Trim(),
            (insurerVersionRaw ?? string.Empty).Trim(),
            confidenceScore,
            state,
            createdAt));
    }

    public void Approve(Guid by, DateTime at)
    {
        EnsureUtc(at);
        ReviewState = ReviewState.Approved;
        ReviewedByAgentId = by;
        ReviewedAt = at;
    }

    public void Reject(Guid by, DateTime at)
    {
        EnsureUtc(at);
        ReviewState = ReviewState.Rejected;
        ReviewedByAgentId = by;
        ReviewedAt = at;
    }

    private static void EnsureUtc(DateTime at)
    {
        if (at.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(at));
        }
    }
}
