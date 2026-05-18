using McBrokers.Application.Admin;
using McBrokers.Application.Catalog.Importers;
using McBrokers.Domain.Insurers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Admin.Catalog;

public class ImportModel : PageModel
{
    private readonly ListInsurers _listInsurers;
    private readonly RunAxaDxnCatalogImport _runAxaDxnImport;

    public ImportModel(ListInsurers listInsurers, RunAxaDxnCatalogImport runAxaDxnImport)
    {
        _listInsurers = listInsurers;
        _runAxaDxnImport = runAxaDxnImport;
    }

    public IReadOnlyList<InsurerView> Insurers { get; private set; } = Array.Empty<InsurerView>();
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }
    public RunAxaDxnCatalogImportResult? LastResult { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Insurers = await _listInsurers.ExecuteAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAxaDxnAsync(Guid insurerId, CancellationToken cancellationToken)
    {
        var result = await _runAxaDxnImport.ExecuteAsync(insurerId, cancellationToken);

        if (result.IsSuccess)
        {
            LastResult = result.Value;
            Message =
                $"Importación AXA DXN OK · Batch {result.Value.BatchId} · " +
                $"Total {result.Value.Total} · Auto {result.Value.AutoApproved} · " +
                $"Pendientes {result.Value.PendingReview} · Rechazados {result.Value.Rejected}.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        Insurers = await _listInsurers.ExecuteAsync(cancellationToken);
        return Page();
    }

    public static bool IsSupported(InsurerCode code) => code == InsurerCode.AxaDxn;
}
