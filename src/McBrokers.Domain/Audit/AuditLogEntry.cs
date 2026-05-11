using McBrokers.SharedKernel;

namespace McBrokers.Domain.Audit;

public sealed class AuditLogEntry
{
    public Guid Id { get; }
    public Guid? AgentId { get; }
    public string Action { get; }
    public string EntityType { get; }
    public string EntityId { get; }
    public string? CorrelationId { get; }
    public string PayloadJson { get; }
    public DateTime CreatedAt { get; }

    private AuditLogEntry(
        Guid id,
        Guid? agentId,
        string action,
        string entityType,
        string entityId,
        string? correlationId,
        string payloadJson,
        DateTime createdAt)
    {
        Id = id;
        AgentId = agentId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        CorrelationId = correlationId;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
    }

    public static Result<AuditLogEntry> Create(
        Guid? agentId,
        string action,
        string entityType,
        string entityId,
        string? correlationId,
        string payloadJson,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return Result<AuditLogEntry>.Failure("Audit action must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Result<AuditLogEntry>.Failure("Audit entityType must not be empty.");
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            return Result<AuditLogEntry>.Failure("Audit createdAt must be UTC.");
        }

        return Result<AuditLogEntry>.Success(new AuditLogEntry(
            Guid.NewGuid(),
            agentId,
            action,
            entityType,
            entityId ?? string.Empty,
            correlationId,
            payloadJson ?? "{}",
            createdAt));
    }
}
