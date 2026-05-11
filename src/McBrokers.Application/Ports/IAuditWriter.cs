namespace McBrokers.Application.Ports;

public interface IAuditWriter
{
    Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? payload,
        CancellationToken cancellationToken);
}
