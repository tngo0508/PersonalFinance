using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// The Guild Hall page's own wording around user content, in the request's Language. User content itself (titles,
/// bodies, comments, Pseudonyms, poll questions) is never translated (spec §1).
/// </summary>
public static class CommunityText
{
    private const string CategoryKeyPrefix = "Community.Category.";

    /// <summary>
    /// The Strategy categories the board's chips and the pin form offer. The stored value stays English; only the
    /// label is translated.
    /// </summary>
    public static IReadOnlyList<string> StrategyCategories => CommunityStrategyLimits.Categories;

    /// <summary>Every category with a translated label: the Strategy categories plus the poll category "Goals".</summary>
    private static readonly HashSet<string> TranslatedCategories =
        new(CommunityStrategyLimits.Categories.Append("Goals"), StringComparer.Ordinal);

    /// <summary>How long before <paramref name="nowUtc"/> something was posted: "just now", "5m ago", "3h ago", "2d ago".</summary>
    public static string TimeAgo(IStringLocalizer localizer, DateTime postedUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        TimeSpan span = nowUtc - postedUtc;

        if (span.TotalMinutes < 1)
        {
            return localizer["Community.Js.JustNow"];
        }

        if (span.TotalMinutes < 60)
        {
            return localizer.Named("Community.TimeAgo.Minutes", ("count", (int)span.TotalMinutes));
        }

        if (span.TotalHours < 24)
        {
            return localizer.Named("Community.TimeAgo.Hours", ("count", (int)span.TotalHours));
        }

        return localizer.Named("Community.TimeAgo.Days", ("count", (int)span.TotalDays));
    }

    /// <summary>
    /// The label of a poll's or Strategy's category: translated when it is one the app defines (a Strategy category
    /// or "Goals"), otherwise shown as stored.
    /// </summary>
    public static string Category(IStringLocalizer localizer, string category)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return TranslatedCategories.Contains(category)
            ? localizer[CategoryKeyPrefix + category]
            : category;
    }

    /// <summary>
    /// Who a post is shown as written by: the author's Pseudonym, or the translated "Former member" when the author
    /// has none any more because the Account was purged (ADR 0005, ADR 0018).
    /// </summary>
    public static string Author(IStringLocalizer localizer, string? pseudonym)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return string.IsNullOrWhiteSpace(pseudonym) ? localizer["Community.FormerMember"] : pseudonym;
    }
}
