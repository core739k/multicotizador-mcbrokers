using McBrokers.Application.Admin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Insurers;

public class IndexModel : PageModel
{
    private readonly ListInsurers _listInsurers;

    public IndexModel(ListInsurers listInsurers) => _listInsurers = listInsurers;

    public IReadOnlyList<InsurerView> Insurers { get; private set; } = Array.Empty<InsurerView>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Insurers = await _listInsurers.ExecuteAsync(cancellationToken);
    }
}
