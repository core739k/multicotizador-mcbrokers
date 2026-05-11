using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using McBrokers.Application.Catalog;
using McBrokers.Application.Quotations;
using McBrokers.Domain.Quotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Cotizacion;

public class IndexModel : PageModel
{
    private readonly RequestQuotation _request;
    private readonly GetCatalogForYear _catalog;

    public IndexModel(RequestQuotation request, GetCatalogForYear catalog)
    {
        _request = request;
        _catalog = catalog;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<VehicleMasterView> AvailableVehicles { get; private set; } = Array.Empty<VehicleMasterView>();
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadVehiclesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadVehiclesAsync(cancellationToken);
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
            Deductibles = new
            {
                MaterialDamagesDeductiblePct = 5m,
                RobberyDeductiblePct = 10m,
                MedicalExpensesSumInsured = 200_000m,
                CivilLiabilitySumInsured = 3_000_000m,
            },
        });

        var result = await _request.ExecuteAsync(
            new RequestQuotationCommand(
                Input.VehicleMasterId,
                Input.Package,
                Input.PaymentMode,
                Input.ValuationType,
                Input.SumInsured,
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
        var year = DateTime.UtcNow.Year;
        var view = await _catalog.ExecuteAsync(year, cancellationToken);
        AvailableVehicles = view.Vehicles;
    }

    public class InputModel
    {
        [Required] public Guid VehicleMasterId { get; set; }
        [Required] public PackageCode Package { get; set; } = PackageCode.Amplia;
        [Required] public PaymentMode PaymentMode { get; set; } = PaymentMode.Annual;
        [Required] public ValuationType ValuationType { get; set; } = ValuationType.Commercial;
        [Range(0.01, double.MaxValue)] public decimal SumInsured { get; set; } = 250_000m;
        [Required, RegularExpression(@"^\d{5}$")] public string PostalCode { get; set; } = "06700";
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastNamePaternal { get; set; } = string.Empty;
        public string LastNameMaternal { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; } = new(1990, 1, 1);
        public Gender Gender { get; set; } = Gender.Male;
    }
}
