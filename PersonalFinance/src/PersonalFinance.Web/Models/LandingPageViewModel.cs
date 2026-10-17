namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the public Landing Page shown to signed-out visitors at the root URL.
/// Contains only static product content; no user data and no API calls.
/// </summary>
public sealed class LandingPageViewModel
{
    /// <summary>
    /// Gets the product features presented in the Landing Page feature grid, in display order.
    /// </summary>
    public IReadOnlyList<LandingFeature> Features { get; init; } = Array.Empty<LandingFeature>();

    /// <summary>
    /// Creates the Landing Page content describing the application's real feature set.
    /// </summary>
    public static LandingPageViewModel Create() => new()
    {
        Features =
        [
            new("Net Worth", "trending-up",
                "Project your net worth to retirement across base, conservative, and aggressive scenarios."),
            new("Budget", "pie-chart",
                "Split your monthly income across ten spending categories and see where every dollar goes."),
            new("Savings Goals", "target",
                "Set a target for each goal and track how much you have saved toward it."),
            new("Spreadsheet Analysis", "bar-chart-3",
                "Turn your budget spreadsheets into monthly income, expense, and category breakdowns."),
            new("Investment Calculator", "calculator",
                "Model compound growth with your own contributions, return rate, and time horizon."),
            new("Finance News", "newspaper",
                "Catch up on market and personal finance headlines without leaving the app."),
            new("Community", "users",
                "Vote in polls and read stories from others working toward financial independence."),
            new("Google Drive Integration", "folder-open",
                "Connect a Drive folder and keep your budget spreadsheets in sync for analysis."),
            new("AI Assistant", "sparkles",
                "Ask questions about budgeting, saving, and investing in plain language.")
        ]
    };
}

/// <summary>
/// A single feature tile on the Landing Page.
/// </summary>
/// <param name="Title">Feature name.</param>
/// <param name="Icon">Lucide icon name.</param>
/// <param name="Description">One-sentence description of the feature.</param>
public sealed record LandingFeature(string Title, string Icon, string Description);
