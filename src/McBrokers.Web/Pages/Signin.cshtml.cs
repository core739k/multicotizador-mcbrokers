using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace McBrokers.Web.Pages;

public class SigninModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        return Page();
    }

    public IActionResult OnPost(string? returnUrl = null) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            GoogleDefaults.AuthenticationScheme);
}
