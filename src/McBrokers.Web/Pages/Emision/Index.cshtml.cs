using System.ComponentModel.DataAnnotations;
using McBrokers.Application.Emissions;
using McBrokers.Application.Ports;
using McBrokers.Application.Validation;
using McBrokers.Domain.Emissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Emision;

public class IndexModel : PageModel
{
    private readonly EmitPolicy _emit;
    private readonly IQuotationRepository _quotations;
    private readonly IPostalCodeResolver _postal;

    public IndexModel(
        EmitPolicy emit,
        IQuotationRepository quotations,
        IPostalCodeResolver postal)
    {
        _emit = emit;
        _quotations = quotations;
        _postal = postal;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    // Hidratado server-side cuando entras a la página — el CP del Quotation
    // se pre-llena y el resolver SEPOMEX nos da Estado/Municipio/Asentamientos
    // para el primer paint. El JS los re-puebla cuando el vendedor cambia el CP.
    public IReadOnlyList<string> AvailableColonias { get; private set; } = Array.Empty<string>();
    public string ResolvedEstado { get; private set; } = string.Empty;
    public string ResolvedMunicipio { get; private set; } = string.Empty;

    public async Task OnGetAsync(Guid resultId, CancellationToken cancellationToken)
    {
        Input.QuotationInsurerResultId = resultId;

        // Pre-fill desde la Quotation que dio origen al result.
        var quotation = await _quotations.FindByResultIdAsync(resultId, cancellationToken);
        if (quotation is not null)
        {
            Input.PostalCode = quotation.PostalCode;
            await TryHydrateSepomexAsync(quotation.PostalCode, cancellationToken);
        }
    }

    // Handler AJAX: el JS lo invoca con fetch('?handler=ResolveCp&cp=12345').
    // Devuelve JSON plano con Estado/Municipio/Asentamientos o el error.
    public async Task<IActionResult> OnGetResolveCpAsync(string cp, CancellationToken cancellationToken)
    {
        var result = await _postal.ResolveAsync(cp, cancellationToken);
        if (!result.IsSuccess)
        {
            return new JsonResult(new { ok = false, error = result.Error })
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
        }

        return new JsonResult(new
        {
            ok = true,
            estado = result.Value.Estado,
            municipio = result.Value.Municipio,
            asentamientos = result.Value.Asentamientos.Select(a => a.Colonia).ToList(),
        });
    }

    public async Task<IActionResult> OnPostAsync(Guid resultId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Input.QuotationInsurerResultId = resultId;
            await TryHydrateSepomexAsync(Input.PostalCode, cancellationToken);
            return Page();
        }

        var result = await _emit.ExecuteAsync(
            new EmitPolicyCommand(
                resultId,
                new EmissionCustomerSnapshot(
                    Input.FirstName, Input.LastNamePaternal, Input.LastNameMaternal,
                    Input.Rfc, Input.Street, Input.ExteriorNumber, Input.InteriorNumber,
                    Input.Neighborhood, Input.City, Input.StateCode, Input.PostalCode,
                    Input.Phone, Input.Email,
                    Input.Plate, Input.EngineNumber, Input.SerialNumber)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            Input.QuotationInsurerResultId = resultId;
            await TryHydrateSepomexAsync(Input.PostalCode, cancellationToken);
            return Page();
        }

        if (result.Value.Status == EmissionStatus.Issued)
        {
            return RedirectToPage(
                "/Emision/Confirmacion",
                new { emissionId = result.Value.EmissionId });
        }

        ErrorMessage = "La aseguradora rechazó la emisión. Revisa los detalles capturados o intenta más tarde.";
        Input.QuotationInsurerResultId = resultId;
        await TryHydrateSepomexAsync(Input.PostalCode, cancellationToken);
        return Page();
    }

    private async Task TryHydrateSepomexAsync(string? cp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cp)) return;
        var resolved = await _postal.ResolveAsync(cp, ct);
        if (!resolved.IsSuccess) return;

        ResolvedEstado = resolved.Value.Estado;
        ResolvedMunicipio = resolved.Value.Municipio;
        AvailableColonias = resolved.Value.Asentamientos.Select(a => a.Colonia).ToList();

        // Solo seteamos si están vacíos — respetamos lo que el vendedor ya hubiera
        // escogido en una submission previa fallida.
        if (string.IsNullOrWhiteSpace(Input.StateCode)) Input.StateCode = resolved.Value.Estado;
        if (string.IsNullOrWhiteSpace(Input.City)) Input.City = resolved.Value.Municipio;
    }

    public class InputModel
    {
        public Guid QuotationInsurerResultId { get; set; }

        [Display(Name = "Nombre")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Apellido paterno")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string LastNamePaternal { get; set; } = string.Empty;

        [Display(Name = "Apellido materno")]
        public string LastNameMaternal { get; set; } = string.Empty;

        [Display(Name = "RFC")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        [StringLength(13, MinimumLength = 12, ErrorMessage = ValidationMessages.Rfc)]
        public string Rfc { get; set; } = string.Empty;

        [Display(Name = "Teléfono")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        [RegularExpression(@"^\d{10}$", ErrorMessage = ValidationMessages.Phone)]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Calle")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string Street { get; set; } = string.Empty;

        [Display(Name = "Número exterior")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string ExteriorNumber { get; set; } = string.Empty;

        [Display(Name = "Número interior")]
        public string? InteriorNumber { get; set; }

        [Display(Name = "Colonia")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string Neighborhood { get; set; } = string.Empty;

        [Display(Name = "Ciudad/Municipio")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string City { get; set; } = string.Empty;

        [Display(Name = "Estado")]
        public string StateCode { get; set; } = string.Empty;

        [Display(Name = "Código postal")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        [RegularExpression(@"^\d{5}$", ErrorMessage = ValidationMessages.PostalCode)]
        public string PostalCode { get; set; } = string.Empty;

        [Display(Name = "Placas")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string Plate { get; set; } = string.Empty;

        [Display(Name = "Número de motor")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string EngineNumber { get; set; } = string.Empty;

        [Display(Name = "Número de serie")]
        [Required(ErrorMessage = ValidationMessages.Required)]
        public string SerialNumber { get; set; } = string.Empty;
    }
}
