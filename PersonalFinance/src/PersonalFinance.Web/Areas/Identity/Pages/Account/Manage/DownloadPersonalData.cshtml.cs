using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Replaces the Identity UI default download, which exported only the sign-in fields plus the authenticator key.
/// The Data Export on the Personal data page is the only download; this address just sends the user there.
/// </summary>
[Authorize]
public class DownloadPersonalDataModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("./PersonalData");
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("./PersonalData");
    }
}
