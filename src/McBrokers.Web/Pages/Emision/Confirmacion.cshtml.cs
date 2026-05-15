using System.ComponentModel.DataAnnotations;
using McBrokers.Application.Emissions;
using McBrokers.Application.Ports;
using McBrokers.Application.Validation;
using McBrokers.Domain.Emissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Emision;

// Página de confirmación tras emisión exitosa. Muestra el número de inciso
// devuelto por la aseguradora, el PDF embebido y permite descargarlo o
// enviarlo por correo. Replicación del visor de Quotation Details, pero
// para una sola póliza ya emitida.
//
// Nota negocio: COPSIS devuelve <inciso>N</inciso> en la respuesta de emisión
// AXA DXN. "Inciso" en AXA suele ser número de item/endoso, no de póliza —
// pendiente confirmar con negocio si debe mostrarse como "Póliza" en alguna
// vista posterior. Por ahora la etiqueta visible es "Número de inciso".
public class ConfirmacionModel : PageModel
{
    private readonly IEmissionRepository _emissions;
    private readonly IBlobStore _blob;
    private readonly SendPolicyEmail _send;

    public ConfirmacionModel(
        IEmissionRepository emissions, IBlobStore blob, SendPolicyEmail send)
    {
        _emissions = emissions;
        _blob = blob;
        _send = send;
    }

    public Emission? Emission { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [EmailAddress(ErrorMessage = ValidationMessages.Email)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid emissionId, CancellationToken ct)
    {
        Emission = await _emissions.GetByIdAsync(emissionId, ct);
        if (Emission is null) return NotFound();
        return Page();
    }

    // Handler para iframe/descarga. Iframe usa ?handler=Pdf (inline);
    // botón de descarga usa ?handler=Pdf&download=true (attachment).
    public async Task<IActionResult> OnGetPdfAsync(Guid emissionId, bool download, CancellationToken ct)
    {
        var emission = await _emissions.GetByIdAsync(emissionId, ct);
        if (emission is null || string.IsNullOrWhiteSpace(emission.PdfBlobRef))
        {
            return NotFound();
        }

        var bytes = await _blob.ReadBinaryAsync(emission.PdfBlobRef, ct);
        if (bytes is null) return NotFound();

        var fileName = $"poliza-{emission.PolicyNumber ?? emission.Id.ToString("n")}.pdf";
        if (download)
        {
            return File(bytes, "application/pdf", fileDownloadName: fileName);
        }
        // Inline para el iframe.
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(bytes, "application/pdf");
    }

    public async Task<IActionResult> OnPostEnviarCorreoAsync(Guid emissionId, CancellationToken ct)
    {
        Emission = await _emissions.GetByIdAsync(emissionId, ct);
        if (Emission is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        var result = await _send.ExecuteAsync(new SendPolicyEmailCommand(emissionId, Email), ct);
        if (result.IsSuccess)
        {
            StatusMessage = $"Póliza enviada a {Email}.";
        }
        else
        {
            ErrorMessage = result.Error;
        }
        return Page();
    }
}
