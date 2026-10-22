namespace PersonalFinance.Web.Models;

/// <summary>
/// The Unavailable State: a page's data could not be loaded. Shown instead of the page, never as an empty state.
/// </summary>
public sealed class UnavailableStateViewModel
{
    /// <summary>Gets the page title shown in the browser tab and the page heading (e.g. "Budget Planner").</summary>
    public string PageTitle { get; init; } = string.Empty;

    /// <summary>Gets what could not be loaded, e.g. "We couldn't load your budget".</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Gets the controller whose action the Retry link reloads.</summary>
    public string RetryController { get; init; } = string.Empty;

    /// <summary>Gets the action the Retry link reloads; Index unless the page is another action.</summary>
    public string RetryAction { get; init; } = "Index";
}
