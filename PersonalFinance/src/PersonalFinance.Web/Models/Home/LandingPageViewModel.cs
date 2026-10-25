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
        CoreFeatures = [new("NetWorth"), new("Budget"), new("SavingsGoals")],
        MoreFeatures =
        [
            new("SpreadsheetAnalysis"), new("InvestmentCalculator"), new("GoogleDrive"), new("AiAssistant"),
            new("FinanceNews"), new("Community")
        ]
    };
}

/// <summary>
/// A single feature entry on the Landing Page. Its name and description are localizer keys under
/// <see cref="KeyPrefix"/> (<c>Landing.Feature.NetWorth.Title</c>), so the view shows them in the visitor's language.
/// </summary>
/// <param name="Id">The feature's key segment, for example <c>NetWorth</c>.</param>
public sealed record LandingFeature(string Id)
{
    /// <summary>The prefix of every Landing Page feature key in <c>en.json</c>.</summary>
    public const string KeyPrefix = "Landing.Feature.";

    /// <summary>Gets the localizer key of the feature name.</summary>
    public string TitleKey => $"{KeyPrefix}{Id}.Title";

    /// <summary>Gets the localizer key of the one-sentence description.</summary>
    public string DescriptionKey => $"{KeyPrefix}{Id}.Description";
}
