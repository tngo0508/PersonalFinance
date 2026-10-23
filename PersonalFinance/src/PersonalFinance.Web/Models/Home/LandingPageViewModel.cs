namespace PersonalFinance.Web.Models.Home;

/// <summary>
/// View model for the public Landing Page shown to signed-out visitors at the root URL.
/// Contains only static product content; no user data and no API calls.
/// </summary>
public sealed class LandingPageViewModel
{
    /// <summary>
    /// Gets the three tools that build a plan (Net Worth, Budget, Savings Goals), in display order.
    /// </summary>
    public IReadOnlyList<LandingFeature> CoreFeatures { get; init; } = Array.Empty<LandingFeature>();

    /// <summary>
    /// Gets the supporting tools listed under "Also included", in display order.
    /// </summary>
    public IReadOnlyList<LandingFeature> MoreFeatures { get; init; } = Array.Empty<LandingFeature>();

    /// <summary>
    /// Creates the Landing Page content describing the application's real feature set.
    /// </summary>
    public static LandingPageViewModel Create() => new()
    {
        CoreFeatures =
        [
            new("Net Worth",
                "Enter your savings, income and spending. See your projection to retirement in base, conservative, and aggressive scenarios."),
            new("Budget",
                "Split your monthly income across ten spending categories, so you know how much is left to save."),
            new("Savings Goals",
                "Set a target for each goal and track how much you have saved toward it.")
        ],
        MoreFeatures =
        [
            new("Spreadsheet Analysis",
                "Turn your budget spreadsheets into monthly income, expense, and category breakdowns."),
            new("Investment Calculator",
                "Model compound growth with your own contributions, return rate, and time horizon."),
            new("Google Drive Integration",
                "Connect a Drive folder and keep your budget spreadsheets in sync for analysis."),
            new("AI Assistant",
                "Ask questions about budgeting, saving, and investing in plain language."),
            new("Finance News",
                "Catch up on market and personal finance headlines without leaving the app."),
            new("Community",
                "Vote in polls and read stories from others working toward financial independence.")
        ]
    };
}

/// <summary>
/// A single feature entry on the Landing Page.
/// </summary>
/// <param name="Title">Feature name.</param>
/// <param name="Description">One-sentence description of the feature.</param>
public sealed record LandingFeature(string Title, string Description);
