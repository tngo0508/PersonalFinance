using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Renders the Unavailable State for a page whose data could not be loaded.
/// </summary>
public static class UnavailableStateExtensions
{
    /// <summary>View name of the Unavailable State (<c>Views/Shared/Unavailable.cshtml</c>).</summary>
    public const string UnavailableViewName = "Unavailable";

    /// <summary>
    /// Returns the Unavailable State instead of the page, so a failed load never shows defaults or an empty state
    /// that the user could mistake for their data (or save over it).
    /// </summary>
    public static ViewResult UnavailableView(this Controller controller, string pageTitle, string heading,
        string retryController)
    {
        return controller.View(UnavailableViewName, new UnavailableStateViewModel
        {
            PageTitle = pageTitle,
            Heading = heading,
            RetryController = retryController
        });
    }
}
