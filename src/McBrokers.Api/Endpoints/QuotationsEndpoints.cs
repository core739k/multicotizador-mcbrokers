using McBrokers.Application.Quotations;

namespace McBrokers.Api.Endpoints;

public static class QuotationsEndpoints
{
    public static IEndpointRouteBuilder MapQuotations(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quotations")
            .RequireAuthorization()
            .WithTags("Quotations");

        group.MapPost("", CreateQuotation);
        group.MapGet("/{id:guid}", GetStatus);

        return app;
    }

    private static async Task<IResult> CreateQuotation(
        RequestQuotationCommand command,
        HttpContext httpContext,
        RequestQuotation handler,
        CancellationToken ct)
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].ToString();

        var result = await handler.ExecuteAsync(command, correlationId, ct);
        if (!result.IsSuccess)
        {
            return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        httpContext.Response.Headers["X-Correlation-Id"] = result.Value.CorrelationId;
        return Results.Accepted(
            uri: $"/api/v1/quotations/{result.Value.QuotationId}",
            value: new { quotationId = result.Value.QuotationId, correlationId = result.Value.CorrelationId });
    }

    private static async Task<IResult> GetStatus(Guid id, GetQuotationStatus handler, CancellationToken ct)
    {
        var view = await handler.ExecuteAsync(id, ct);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
