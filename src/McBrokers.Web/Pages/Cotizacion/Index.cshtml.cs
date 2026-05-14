using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using McBrokers.Application.Admin;
using McBrokers.Application.Catalog;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Quotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace McBrokers.Web.Pages.Cotizacion;

public class IndexModel : PageModel
{
    private readonly RequestQuotation _request;
    private readonly GetCatalogForYear _catalog;
    private readonly SearchVehiclesByText _search;
    private readonly ListInsurers _insurers;
    private readonly DefaultCoverages _defaultCoverages;

    public IndexModel(
        RequestQuotation request,
        GetCatalogForYear catalog,
        SearchVehiclesByText search,
        ListInsurers insurers,
        IOptions<DefaultCoverages> defaultCoverages)
    {
        _request = request;
        _catalog = catalog;
        _search = search;
        _insurers = insurers;
        _defaultCoverages = defaultCoverages.Value;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // El año del catálogo. Soporta ?year= en GET y se preserva en POST
    // porque la query string viaja en el action="" del form.
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    // Query libre del fallback "No encuentro el vehículo".
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Vehículo elegido desde el panel de fallback — pre-selecciona el dropdown.
    [BindProperty(SupportsGet = true)]
    public Guid? SelectedVehicleId { get; set; }

    public int EffectiveYear => Year ?? DateTime.UtcNow.Year;

    public IReadOnlyList<VehicleMasterView> AvailableVehicles { get; private set; } = Array.Empty<VehicleMasterView>();
    public IReadOnlyList<VehicleSearchResultRow> SearchResults { get; private set; } = Array.Empty<VehicleSearchResultRow>();
    public bool SearchRan { get; private set; }
    public IReadOnlyDictionary<Guid, string> InsurerNamesById { get; private set; } = new Dictionary<Guid, string>();
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadVehiclesAsync(cancellationToken);
        await LoadInsurerNamesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            await RunFallbackSearchAsync(cancellationToken);
        }

        if (SelectedVehicleId.HasValue)
        {
            Input.VehicleMasterId = SelectedVehicleId.Value;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadVehiclesAsync(cancellationToken);
        await LoadInsurerNamesAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();

        var snapshot = JsonSerializer.Serialize(new
        {
            Contractor = new
            {
                Input.FirstName,
                Input.LastNamePaternal,
                Input.LastNameMaternal,
                PostalCode = Input.PostalCode,
                Input.Gender,
                Input.DateOfBirth,
            },
            HabitualDriver = new
            {
                PostalCode = Input.PostalCode,
                Input.Gender,
                Input.DateOfBirth,
            },
            // Fase A (#4): los defaults viven en appsettings.json bajo
            // "Cotizacion:DefaultCoverages". Fase B agregará overrides por
            // aseguradora y captura desde admin — ver REQUIREMENTS.md §4.2.
            Deductibles = new
            {
                _defaultCoverages.MaterialDamagesDeductiblePct,
                _defaultCoverages.RobberyDeductiblePct,
                _defaultCoverages.MedicalExpensesSumInsured,
                _defaultCoverages.CivilLiabilitySumInsured,
            },
        });

        // SumInsured placeholder — el form ya no lo expone. Para Commercial
        // (default) los adapters mandan "0" al WS y la aseguradora calcula.
        // Cuando agreguemos selector de ValuationType al wizard + override
        // de SumInsured por card en la pantalla de resultados (Fase 2),
        // este placeholder dejará de aplicar.
        const decimal SumInsuredPlaceholder = 250_000m;

        var result = await _request.ExecuteAsync(
            new RequestQuotationCommand(
                Input.VehicleMasterId,
                Input.Package,
                Input.PaymentMode,
                Input.ValuationType,
                SumInsuredPlaceholder,
                Input.PostalCode,
                snapshot),
            correlationId: null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        return RedirectToPage("./Status", new { id = result.Value.QuotationId });
    }

    private async Task LoadVehiclesAsync(CancellationToken cancellationToken)
    {
        var view = await _catalog.ExecuteAsync(EffectiveYear, cancellationToken);
        AvailableVehicles = view.Vehicles;
    }

    private async Task LoadInsurerNamesAsync(CancellationToken cancellationToken)
    {
        var insurers = await _insurers.ExecuteAsync(cancellationToken);
        InsurerNamesById = insurers.ToDictionary(i => i.Id, i => i.Name);
    }

    private async Task RunFallbackSearchAsync(CancellationToken cancellationToken)
    {
        SearchRan = true;
        var insurers = await _insurers.ExecuteAsync(cancellationToken);
        var enabledIds = insurers.Where(i => i.IsEnabled).Select(i => i.Id).ToList();
        var results = await _search.ExecuteAsync(EffectiveYear, Q, enabledIds, cancellationToken);
        SearchResults = results.Items;
    }

    public class InputModel
    {
        [Required] public Guid VehicleMasterId { get; set; }
        [Required] public PackageCode Package { get; set; } = PackageCode.Amplia;
        [Required] public PaymentMode PaymentMode { get; set; } = PaymentMode.Annual;
        [Required] public ValuationType ValuationType { get; set; } = ValuationType.Commercial;
        // SumInsured se quitó del form: para ValuationType=Commercial (caso
        // default del wizard) la aseguradora calcula el valor desde su
        // tarifa interna y el SumInsured que mandamos es ignorado (ver
        // ValuationTypeExtensions.ShouldSendSumInsured — Commercial → "0").
        // Para Agreed/Invoice, el vendedor lo ajustará desde el card de
        // resultado (override por aseguradora) en una iteración futura.
        // Mientras tanto un placeholder permite construir el comando.
        [Required, RegularExpression(@"^\d{5}$")] public string PostalCode { get; set; } = "06700";
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastNamePaternal { get; set; } = string.Empty;
        public string LastNameMaternal { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; } = new(1990, 1, 1);
        public Gender Gender { get; set; } = Gender.Male;
    }
}
