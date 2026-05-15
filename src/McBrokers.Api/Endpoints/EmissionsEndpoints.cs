using McBrokers.Application.Emissions;
using McBrokers.Application.Ports;

namespace McBrokers.Api.Endpoints;

public static class EmissionsEndpoints
{
    public static IEndpointRouteBuilder MapEmissions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/emissions")
            .RequireAuthorization()
            .WithTags("Emissions");

        group.MapPost("", Emit);
        group.MapGet("{id:guid}/pdf", GetPdf);

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

    // Visor de póliza: sirve el PDF descargado al emitir, persistido por
    // EmitPolicy bajo la ruta canónica BlobPaths.PolizaPdf. La ruta relativa
    // se guarda en Emission.PdfBlobRef y se lee aquí vía IBlobStore.ReadBinaryAsync.
    private static async Task<IResult> GetPdf(
        Guid id, IEmissionRepository emissions, IBlobStore blob,
        HttpContext http, CancellationToken ct)
    {
        var emission = await emissions.GetByIdAsync(id, ct);
        if (emission is null || string.IsNullOrWhiteSpace(emission.PdfBlobRef))
        {
            return Results.NotFound();
        }

        var bytes = await blob.ReadBinaryAsync(emission.PdfBlobRef, ct);
        if (bytes is null) return Results.NotFound();

        // Disposition explícita: inline para que el iframe del visor lo
        // muestre embebido, no force descarga. El Web tiene un botón
        // "Descargar PDF" separado que añade ?download=1.
        var fileName = $"poliza-{emission.PolicyNumber ?? emission.Id.ToString("n")}.pdf";
        http.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return Results.Bytes(bytes, "application/pdf");
    }
}
