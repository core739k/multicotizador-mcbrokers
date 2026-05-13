using McBrokers.Application.Agents;

namespace McBrokers.Api.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgents(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agent")
            .RequireAuthorization()
            .WithTags("Agent");

        group.MapGet("/me", async (GetCurrentAgentSummary handler, CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        });

        return app;
    }
}
