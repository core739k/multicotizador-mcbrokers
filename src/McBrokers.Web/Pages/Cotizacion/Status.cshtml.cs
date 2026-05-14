using McBrokers.Application.Quotations;
using McBrokers.Domain.Quotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Cotizacion;

public class StatusModel : PageModel
{
    private readonly GetQuotationStatus _getStatus;
    private readonly RequoteInsurerResult _requote;

    public StatusModel(GetQuotationStatus getStatus, RequoteInsurerResult requote)
    {
        _getStatus = getStatus;
        _requote = requote;
    }

    public QuotationStatusView? View { get; private set; }
    public string? RequoteError { get; private set; }

    public async Task OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        View = await _getStatus.ExecuteAsync(id, cancellationToken);
    }

    // Handler para el form de re-cotización por card. Cada select hace
    // this.form.submit() onchange; los demás overrides viajan como hidden
    // inputs para no resetearse cuando solo se toca uno.
    public async Task<IActionResult> OnPostRequoteAsync(
        Guid id, Guid insurerId,
        Guid? vehicleMasterId,
        ValuationType? valuation,
        decimal? dmPct,
        decimal? rtPct,
        decimal? gmo,
        CancellationToken cancellationToken)
    {
        var outcome = await _requote.ExecuteAsync(
            new RequoteInsurerCommand(id, insurerId, vehicleMasterId, valuation, dmPct, rtPct, gmo),
            cancellationToken);

        if (!outcome.IsSuccess)
        {
            // Cargo el view de todas formas para que la página renderice con
            // el resto de tarjetas + un alert con el error.
            View = await _getStatus.ExecuteAsync(id, cancellationToken);
            RequoteError = outcome.Error;
            return Page();
        }

        return RedirectToPage("./Status", new { id });
    }
}
