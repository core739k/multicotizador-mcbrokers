using McBrokers.Application.Admin;
using McBrokers.Domain.Insurers;

namespace McBrokers.Api.Endpoints;

public static class AdminInsurersEndpoints
{
    public static IEndpointRouteBuilder MapAdminInsurers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/insurers")
            .RequireAuthorization("RequireAdmin")
            .WithTags("Admin · Insurers");

        group.MapGet("", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapPut("/{id:guid}/configs/{environment}", UpsertConfig);

        return app;
    }

    private static async Task<IResult> List(ListInsurers handler, CancellationToken ct) =>
        Results.Ok(await handler.ExecuteAsync(ct));

    private static async Task<IResult> GetById(Guid id, GetInsurer handler, CancellationToken ct)
    {
        var view = await handler.ExecuteAsync(id, ct);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> Create(CreateInsurerCommand cmd, CreateInsurer handler, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(cmd, ct);
        return result.IsSuccess
            ? Results.Created($"/api/v1/admin/insurers/{result.Value}", new { id = result.Value })
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Update(Guid id, UpdateInsurerCommand body, UpdateInsurer handler, CancellationToken ct)
    {
        if (body.Id != id)
        {
            return Results.Problem("Route id and body id must match.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await handler.ExecuteAsync(body, ct);
        if (result.IsSuccess) return Results.NoContent();

        var status = result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Results.Problem(result.Error, statusCode: status);
    }

    private static async Task<IResult> UpsertConfig(
        Guid id,
        string environment,
        UpsertInsurerConfigBody body,
        UpsertInsurerConfig handler,
        CancellationToken ct)
    {
        if (!Enum.TryParse<InsurerEnvironment>(environment, ignoreCase: true, out var env))
        {
            return Results.Problem($"Unknown environment '{environment}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var cmd = new UpsertInsurerConfigCommand(
            id, env,
            body.EndpointUrl, body.BusinessNumber, body.AgentCode,
            body.KeyVaultSecretName, body.TimeoutSeconds, body.MaxRetries);

        var result = await handler.ExecuteAsync(cmd, ct);
        if (result.IsSuccess) return Results.Ok(new { id = result.Value });

        var status = result.Error.StartsWith("Insurer with id", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Results.Problem(result.Error, statusCode: status);
    }
}

public sealed record UpsertInsurerConfigBody(
    string EndpointUrl,
    string BusinessNumber,
    string AgentCode,
    string KeyVaultSecretName,
    int TimeoutSeconds,
    int MaxRetries);
