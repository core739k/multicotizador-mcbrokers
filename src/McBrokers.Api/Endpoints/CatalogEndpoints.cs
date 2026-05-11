using McBrokers.Application.Catalog;

namespace McBrokers.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder app)
    {
        // Public (any authenticated agent) catalog read endpoint.
        var publicGroup = app.MapGroup("/api/v1/catalog")
            .RequireAuthorization()
            .WithTags("Catalog");

        publicGroup.MapGet("/{year:int}", async (int year, GetCatalogForYear handler, CancellationToken ct) =>
            Results.Ok(await handler.ExecuteAsync(year, ct)));

        // Admin-only endpoints for the review queue.
        var adminGroup = app.MapGroup("/api/v1/admin/catalog")
            .RequireAuthorization("RequireAdmin")
            .WithTags("Admin · Catalog");

        adminGroup.MapGet("/pending", async (int? page, int? pageSize, ListPendingMappings handler, CancellationToken ct) =>
            Results.Ok(await handler.ExecuteAsync(page ?? 1, pageSize ?? 25, ct)));

        adminGroup.MapPost("/mappings/{id:guid}/decision",
            async (Guid id, MappingDecisionBody body, DecideMapping handler, CancellationToken ct) =>
            {
                if (!Enum.TryParse<Decision>(body.Decision, ignoreCase: true, out var decision))
                {
                    return Results.Problem($"Unknown decision '{body.Decision}'.", statusCode: StatusCodes.Status400BadRequest);
                }

                var result = await handler.ExecuteAsync(new DecideMappingCommand(id, decision), ct);
                if (result.IsSuccess) return Results.NoContent();

                var status = result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
                return Results.Problem(result.Error, statusCode: status);
            });

        adminGroup.MapPost("/import",
            async (ImportInsurerCatalogCommand body, ImportInsurerCatalog handler, CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(body, ct);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
            });

        return app;
    }
}

public sealed record MappingDecisionBody(string Decision);
