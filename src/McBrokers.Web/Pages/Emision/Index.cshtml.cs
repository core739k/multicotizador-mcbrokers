using System.ComponentModel.DataAnnotations;
using McBrokers.Application.Emissions;
using McBrokers.Application.Validation;
using McBrokers.Domain.Emissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages.Emision;

public class IndexModel : PageModel
{
    private readonly EmitPolicy _emit;

    public IndexModel(EmitPolicy emit) => _emit = emit;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }
    public string? IssuedPolicyNumber { get; private set; }
    public string? PdfBlobRef { get; private set; }

    public void OnGet(Guid resultId)
    {
        Input.QuotationInsurerResultId = resultId;
    }

    public async Task<IActionResult> OnPostAsync(Guid resultId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Input.QuotationInsurerResultId = resultId;
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
            return Page();
        }

        if (result.Value.Status == EmissionStatus.Issued)
        {
            IssuedPolicyNumber = result.Value.PolicyNumber;
            return Page();
        }

        ErrorMessage = "La aseguradora rechazó la emisión. Revisa los detalles capturados o intenta más tarde.";
        Input.QuotationInsurerResultId = resultId;
        return Page();
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

        // StateCode lo puebla el JS de SEPOMEX server-side antes del POST.
        // No es input directo del vendedor; permanece en el modelo porque el
        // EmissionContactData del adapter lo recibe.
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
