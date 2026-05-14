using McBrokers.Application.Catalog;
using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Catalog;
using McBrokers.Domain.Quotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace McBrokers.Web.Pages.Cotizacion;

public class StatusModel : PageModel
{
    private readonly GetQuotationStatus _getStatus;
    private readonly RequoteInsurerResult _requote;
    private readonly IVehicleMasterRepository _vehicles;
    private readonly DefaultCoverages _defaults;

    public StatusModel(
        GetQuotationStatus getStatus,
        RequoteInsurerResult requote,
        IVehicleMasterRepository vehicles,
        IOptions<DefaultCoverages> defaults)
    {
        _getStatus = getStatus;
        _requote = requote;
        _vehicles = vehicles;
        _defaults = defaults.Value;
    }

    public QuotationStatusView? View { get; private set; }
    public string? RequoteError { get; private set; }

    // Opciones disponibles para los selectores de la card. Las del POC vienen
    // de DefaultCoverages (appsettings); en Fase 2 vendrán de cat_deducibles
    // / cat_valor_estimado en BD.
    public decimal[] AvailableDMPct => _defaults.AvailableDMPct;
    public decimal[] AvailableRTPct => _defaults.AvailableRTPct;
    public decimal[] AvailableGMO => _defaults.AvailableGMO;

    // Versiones del mismo Año+Marca+Modelo. Vacío si el vehículo del Quotation
    // ya no existe — el select queda con la versión actual como única opción.
    public IReadOnlyList<VehicleMaster> AvailableVehicleVersions { get; private set; } = Array.Empty<VehicleMaster>();

    public async Task OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        View = await _getStatus.ExecuteAsync(id, cancellationToken);
        await LoadVehicleVersionsAsync(cancellationToken);
    }

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
            View = await _getStatus.ExecuteAsync(id, cancellationToken);
            await LoadVehicleVersionsAsync(cancellationToken);
            RequoteError = outcome.Error;
            return Page();
        }

        return RedirectToPage("./Status", new { id });
    }

    private async Task LoadVehicleVersionsAsync(CancellationToken ct)
    {
        if (View?.Vehicle is null) return;

        // FindByYearAndBrandAsync espera el brand normalizado en mayúsculas
        // — match con cómo se persiste en VehicleMaster (Brand.Trim().ToUpper()
        // no aplica al storage, pero las marcas vienen mayúsculas del legacy).
        var sameBrand = await _vehicles.FindByYearAndBrandAsync(
            View.Vehicle.Year, View.Vehicle.Brand.ToUpperInvariant(), ct);

        // Filtramos al mismo modelo en memoria: queremos las versiones del
        // mismo Año+Marca+Modelo, no todos los modelos de la marca.
        AvailableVehicleVersions = sameBrand
            .Where(v => v.IsActive && v.Model.Equals(View.Vehicle.Model, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.Version)
            .ToList();
    }
}
