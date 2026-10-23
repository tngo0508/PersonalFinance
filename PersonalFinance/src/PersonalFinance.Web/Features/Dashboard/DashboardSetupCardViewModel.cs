namespace PersonalFinance.Web.Features.Dashboard;

/// <summary>
/// A guided next step shown when part of the user's data is missing
/// (rendered by <c>Views/Dashboard/_SetupCard.cshtml</c>).
/// </summary>
public class DashboardSetupCardViewModel
{
    /// <summary>Card heading, e.g. "Set up net worth".</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>One sentence explaining why the step matters.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Call-to-action button text.</summary>
    public string ActionText { get; init; } = string.Empty;

    /// <summary>Lucide icon name.</summary>
    public string Icon { get; init; } = "sparkles";

    /// <summary>MVC controller the call to action targets.</summary>
    public string Controller { get; init; } = string.Empty;

    /// <summary>MVC action the call to action targets.</summary>
    public string Action { get; init; } = "Index";
}
