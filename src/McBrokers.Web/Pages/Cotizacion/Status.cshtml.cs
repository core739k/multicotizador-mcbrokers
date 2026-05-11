using McBrokers.Application.Quotations;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Cotizacion;

public class StatusModel : PageModel
{
    private readonly GetQuotationStatus _getStatus;

    public StatusModel(GetQuotationStatus getStatus) => _getStatus = getStatus;

    public QuotationStatusView? View { get; private set; }

    public async Task OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        View = await _getStatus.ExecuteAsync(id, cancellationToken);
    }
}
