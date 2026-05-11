using System.Text.Json;
using McBrokers.Application.Ports;
using McBrokers.Domain.Audit;
using McBrokers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace McBrokers.Infrastructure.Audit;

public sealed class AuditWriter : IAuditWriter
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentAgentProvider _currentAgent;
    private readonly IHttpContextAccessor _httpContext;

    public AuditWriter(
        AppDbContext db,
        IClock clock,
        ICurrentAgentProvider currentAgent,
        IHttpContextAccessor httpContext)
    {
        _db = db;
        _clock = clock;
        _currentAgent = currentAgent;
        _httpContext = httpContext;
    }

    public async Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? payload,
        CancellationToken cancellationToken)
    {
        Guid? agentId = TryGetAgentId();
        var correlationId = TryGetCorrelationId();
        var payloadJson = payload is null ? "{}" : JsonSerializer.Serialize(payload, JsonOpts);

        var entry = AuditLogEntry.Create(
            agentId,
            action,
            entityType,
            entityId,
            correlationId,
            payloadJson,
            _clock.UtcNow);

        if (!entry.IsSuccess)
        {
            // No bloqueamos la operación principal por un error de audit;
            // pero sí dejamos rastro. En F6 esto irá a Serilog estructurado.
            return;
        }

        await _db.AuditLog.AddAsync(entry.Value, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Guid? TryGetAgentId()
    {
        try
        {
            return _currentAgent.AgentId;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private string? TryGetCorrelationId()
    {
        var httpContext = _httpContext.HttpContext;
        if (httpContext is null) return null;

        var fromHeader = httpContext.Request.Headers[CorrelationHeader].ToString();
        return string.IsNullOrWhiteSpace(fromHeader) ? null : fromHeader;
    }
}
