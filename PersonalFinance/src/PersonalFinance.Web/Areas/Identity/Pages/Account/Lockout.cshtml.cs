using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Shown when sign-in is refused because the Account is locked out. Replaces the Identity UI default so an Account
/// with a pending Deletion Request learns how to cancel it.
/// </summary>
[AllowAnonymous]
public class LockoutModel : PageModel
{
    public void OnGet()
    {
    }
}
