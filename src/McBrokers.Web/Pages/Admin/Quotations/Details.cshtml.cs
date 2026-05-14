using System.Text;
using McBrokers.Application.Admin;
using McBrokers.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Quotations;

public class DetailsModel : PageModel
{
    private readonly GetQuotationAdminDetail _detail;
    private readonly IBlobStore _blob;

    public DetailsModel(GetQuotationAdminDetail detail, IBlobStore blob)
    {
        _detail = detail;
        _blob = blob;
    }

    public QuotationAdminDetailView? View { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        View = await _detail.ExecuteAsync(id, cancellationToken);
        if (View is null) return NotFound();
        return Page();
    }

    // Handler de descarga: el botón "Ver Request/Response" en cada result
    // dispara GET con ?handler=Blob&reference=<urlEncoded>. Devuelve el XML
    // como text/xml inline para que el navegador lo muestre.
    public async Task<IActionResult> OnGetBlobAsync(string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference)) return BadRequest("Referencia vacía.");
        var content = await _blob.ReadAsync(reference, cancellationToken);
        if (content is null) return NotFound("Blob no encontrado.");

        // Decide content-type por el sufijo del blob name (heurística simple —
        // los XMLs los queremos visibles inline, los JSON también).
        var contentType = reference.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? "application/xml"
                        : reference.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json"
                        : "text/plain";

        return Content(content, contentType, Encoding.UTF8);
    }
}
