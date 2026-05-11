using System.ComponentModel.DataAnnotations;
using McBrokers.Application.Admin;
using McBrokers.Domain.Insurers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace McBrokers.Web.Pages.Admin.Insurers;

public class CreateModel : PageModel
{
    private readonly CreateInsurer _createInsurer;

    public CreateModel(CreateInsurer createInsurer) => _createInsurer = createInsurer;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IEnumerable<SelectListItem> CodeOptions =>
        Enum.GetValues<InsurerCode>().Select(c => new SelectListItem(c.ToString(), c.ToString()));

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();

        var result = await _createInsurer.ExecuteAsync(
            new CreateInsurerCommand(Input.Code, Input.Name, Input.DisplayOrder),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        return RedirectToPage("./Edit", new { id = result.Value });
    }

    public class InputModel
    {
        [Required]
        public InsurerCode Code { get; set; }

        [Required, StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }
}
