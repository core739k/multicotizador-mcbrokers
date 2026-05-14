using McBrokers.Application.Admin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Quotations;

public class IndexModel : PageModel
{
    private readonly ListRecentQuotations _list;

    public IndexModel(ListRecentQuotations list) => _list = list;

    public QuotationsPage? Data { get; private set; }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize => 25;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Data = await _list.ExecuteAsync(PageNumber, PageSize, cancellationToken);
    }
}
