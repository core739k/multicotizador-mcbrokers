using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace McBrokers.Infrastructure.Observability;

/// <summary>
/// Garantiza que cada request tenga X-Correlation-Id. Lo lee del header entrante o genera uno nuevo;
/// lo expone en HttpContext.Items["CorrelationId"], en el LogContext de Serilog y en el response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("n");
        }

        context.Items[ContextItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var agentId = context.User?.FindFirst("mcb:agent-id")?.Value;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("AgentId", agentId ?? "anonymous"))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? string.Empty))
        {
            await _next(context).ConfigureAwait(false);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseMcBrokersCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
