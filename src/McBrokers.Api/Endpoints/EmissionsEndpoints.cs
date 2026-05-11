using McBrokers.Application.Emissions;

namespace McBrokers.Api.Endpoints;

public static class EmissionsEndpoints
{
    public static IEndpointRouteBuilder MapEmissions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/emissions")
            .RequireAuthorization()
            .WithTags("Emissions");

        group.MapPost("", Emit);

        return app;
    }

    private static async Task<IResult> Emit(
        EmitPolicyCommand command, EmitPolicy handler, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(command, ct);
        if (!result.IsSuccess)
        {
            return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return result.Value.Status == McBrokers.Domain.Emissions.EmissionStatus.Issued
            ? Results.Ok(new { result.Value.EmissionId, result.Value.PolicyNumber })
            : Results.UnprocessableEntity(new { result.Value.EmissionId, result.Value.Status });
    }
}
