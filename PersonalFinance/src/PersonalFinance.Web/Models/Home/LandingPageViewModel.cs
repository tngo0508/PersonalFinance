namespace PersonalFinance.Web.Models.Home;

/// <summary>
/// View model for the public Landing Page shown to signed-out visitors at the root URL.
/// Contains only static product content; no user data and no API calls.
/// </summary>
public sealed class LandingPageViewModel
{
    /// <summary>
    /// Gets the tools that write the plan itself (the sidebar's "My Finances" group), in display order. Each is a
    /// line of the Landing Page's ledger, with the sample plan's figure where the tool has one.
    /// </summary>
    public IReadOnlyList<LandingFeature> CoreFeatures { get; init; } = Array.Empty<LandingFeature>();

    /// <summary>
    /// Gets the supporting tools (the sidebar's "Resources" group), in display order.
    /// </summary>
    public IReadOnlyList<LandingFeature> MoreFeatures { get; init; } = Array.Empty<LandingFeature>();

    /// <summary>
    /// Creates the Landing Page content describing the application's real feature set.
    /// </summary>
    public static LandingPageViewModel Create() => new()
    {
        CoreFeatures =
        [
            new("NetWorth", HasFigure: true), new("BudgetPlanner", HasFigure: true), new("BudgetTracker", HasFigure: true),
            new("SavingsGoals", HasFigure: true), new("MoneyPriorities"), new("Analysis")
        ],
        MoreFeatures =
        [
            new("InvestmentCalculator"), new("CurrencyConverter"), new("Research"), new("FinanceNews"), new("Community"),
            new("GoogleDrive"), new("AiAssistant")
        ]
    };
}

/// <summary>
/// A single feature entry on the Landing Page. Its name, description and sample figure are localizer keys
/// (<c>Landing.Feature.NetWorth.Title</c>), so the view shows them in the visitor's language.
/// </summary>
/// <param name="Id">The feature's key segment, for example <c>NetWorth</c>.</param>
/// <param name="HasFigure">Whether the sample plan has a figure for this tool (<c>Landing.Feature.{Id}.Figure</c>).</param>
public sealed record LandingFeature(string Id, bool HasFigure = false)
{
    // The prefix of every Landing Page feature key in en.json; the i18n report counts the keys under it as used.
    private const string KeyPrefix = "Landing.Feature.";

    /// <summary>Gets the localizer key of the feature name.</summary>
    public string TitleKey => $"{KeyPrefix}{Id}.Title";

    /// <summary>Gets the localizer key of the one-sentence description.</summary>
    public string DescriptionKey => $"{KeyPrefix}{Id}.Description";

    /// <summary>Gets the localizer key of the sample plan's figure; meaningful only when <see cref="HasFigure"/>.</summary>
    public string FigureKey => $"{KeyPrefix}{Id}.Figure";
}
