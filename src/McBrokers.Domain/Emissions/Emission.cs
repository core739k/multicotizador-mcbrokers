using McBrokers.SharedKernel;

namespace McBrokers.Domain.Emissions;

public enum EmissionStatus
{
    Pending = 0,
    Issued = 1,
    Failed = 2,
}

public sealed class Emission
{
    public Guid Id { get; }
    public Guid QuotationInsurerResultId { get; }
    public Guid AgentId { get; }
    public EmissionStatus Status { get; private set; }
    public string? PolicyNumber { get; private set; }
    public string? PdfBlobRef { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? IssuedAt { get; private set; }

    private Emission(
        Guid id, Guid quotationInsurerResultId, Guid agentId,
        EmissionStatus status, DateTime createdAt)
    {
        Id = id;
        QuotationInsurerResultId = quotationInsurerResultId;
        AgentId = agentId;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Result<Emission> Start(
        Guid quotationInsurerResultId, Guid agentId, DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<Emission>.Failure("createdAt must be UTC.");
        }

        return Result<Emission>.Success(new Emission(
            Guid.NewGuid(), quotationInsurerResultId, agentId,
            EmissionStatus.Pending, createdAt));
    }

    public void MarkIssued(string policyNumber, string? pdfBlobRef, DateTime issuedAt)
    {
        EnsureUtc(issuedAt, nameof(issuedAt));
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new ArgumentException("PolicyNumber must not be empty.", nameof(policyNumber));
        }

        Status = EmissionStatus.Issued;
        PolicyNumber = policyNumber.Trim();
        PdfBlobRef = pdfBlobRef;
        IssuedAt = issuedAt;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = EmissionStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason)
            ? "Unknown emission failure."
            : reason;
    }

    private static void EnsureUtc(DateTime at, string name)
    {
        if (at.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", name);
        }
    }
}
