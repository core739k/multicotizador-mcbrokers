using McBrokers.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Catalog;

public class PendingModel : PageModel
{
    private readonly ListPendingMappings _list;
    private readonly DecideMapping _decide;

    public PendingModel(ListPendingMappings list, DecideMapping decide)
    {
        _list = list;
        _decide = decide;
    }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public PendingMappingsPage PendingPage { get; private set; } = null!;

    public int TotalPages =>
        PendingPage is null
            ? 0
            : (PendingPage.Total + PendingPage.PageSize - 1) / PendingPage.PageSize;

    public string? Message { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PendingPage = await _list.ExecuteAsync(PageNumber, pageSize: 25, cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid mappingId, CancellationToken cancellationToken)
    {
        var result = await _decide.ExecuteAsync(new DecideMappingCommand(mappingId, Decision.Approve), cancellationToken);
        Message = result.IsSuccess ? "Mapping aprobado." : result.Error;
        return RedirectToPage(new { pageNumber = PageNumber });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid mappingId, CancellationToken cancellationToken)
    {
        var result = await _decide.ExecuteAsync(new DecideMappingCommand(mappingId, Decision.Reject), cancellationToken);
        Message = result.IsSuccess ? "Mapping rechazado." : result.Error;
        return RedirectToPage(new { pageNumber = PageNumber });
    }
}
