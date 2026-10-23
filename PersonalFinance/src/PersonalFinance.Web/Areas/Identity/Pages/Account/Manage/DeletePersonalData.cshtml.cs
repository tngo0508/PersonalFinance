using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Replaces the Identity UI default Delete personal data page, which deleted only the sign-in Account and left its
/// data behind. Until the Deletion Request (#142, ADR 0005) is built, this page deletes nothing and points to the
/// Contact page.
/// </summary>
[Authorize]
public class DeletePersonalDataModel : PageModel
{
    public void OnGet()
    {
    }

    /// <summary>
    /// Deletes nothing; shows the page again.
    /// </summary>
    public IActionResult OnPost()
    {
        return RedirectToPage();
    }
}
