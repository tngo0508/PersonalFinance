using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Shown after a Deletion Request, once the Account is signed out: when the Purge happens and how to cancel. The
/// query values only choose what this page says; nothing is looked up or changed.
/// </summary>
[AllowAnonymous]
public class DeletionRequestedModel : PageModel
{
    /// <summary>The Purge date to show, or null when the link carried none.</summary>
    public DateTime? PurgeAfter { get; private set; }

    /// <summary>Whether the cancel link was emailed.</summary>
    public bool EmailSent { get; private set; }

    public void OnGet(string? purgeAfter, bool emailSent = true)
    {
        PurgeAfter = DateTime.TryParseExact(purgeAfter, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime date)
            ? date
            : null;
        EmailSent = emailSent;
    }
}
