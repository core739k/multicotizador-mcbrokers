using McBrokers.Application.Admin;
using McBrokers.Domain.Agents;

namespace McBrokers.Api.Endpoints;

public static class AdminAgentsEndpoints
{
    public static IEndpointRouteBuilder MapAdminAgents(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/agents")
            .RequireAuthorization("RequireAdmin")
            .WithTags("Admin · Agents");

        group.MapGet("", List);
        group.MapPut("/{id:guid}/role", UpdateRole);
        group.MapPut("/{id:guid}/active", SetActive);

        return app;
    }

    private static async Task<IResult> List(ListAgents handler, CancellationToken ct) =>
        Results.Ok(await handler.ExecuteAsync(ct));

    private static async Task<IResult> UpdateRole(
        Guid id,
        UpdateAgentRoleBody body,
        UpdateAgentRole handler,
        CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(new UpdateAgentRoleCommand(id, body.Role), ct);
        if (result.IsSuccess) return Results.NoContent();

        var status = result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Results.Problem(result.Error, statusCode: status);
    }

    private static async Task<IResult> SetActive(
        Guid id,
        SetAgentActiveBody body,
        SetAgentActive handler,
        CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(new SetAgentActiveCommand(id, body.IsActive), ct);
        if (result.IsSuccess) return Results.NoContent();

        var status = result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Results.Problem(result.Error, statusCode: status);
    }
}

public sealed record UpdateAgentRoleBody(AgentRole Role);

public sealed record SetAgentActiveBody(bool IsActive);
