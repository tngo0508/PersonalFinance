using Microsoft.AspNetCore.Mvc.Rendering;

namespace PersonalFinance.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Names of the Account settings sections and which one is current. The built-in Identity UI pages
/// set <c>ViewData["ActivePage"]</c> to one of these names; a page that doesn't falls back to its file name.
/// </summary>
public static class ManageNavPages
{
    public const string Index = "Index";
    public const string Email = "Email";
    public const string ChangePassword = "ChangePassword";
    public const string ExternalLogins = "ExternalLogins";
    public const string TwoFactorAuthentication = "TwoFactorAuthentication";
    public const string PersonalData = "PersonalData";

    /// <summary>
    /// Returns whether <paramref name="page"/> is the section being viewed.
    /// </summary>
    public static bool IsActive(ViewContext viewContext, string page)
    {
        var activePage = viewContext.ViewData["ActivePage"] as string
                         ?? Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
        return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase);
    }
}
