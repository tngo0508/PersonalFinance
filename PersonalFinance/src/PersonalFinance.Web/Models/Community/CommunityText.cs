using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// The Community page's own wording around user content, in the request's Language. User content itself (titles,
/// bodies, comments, author names, poll questions) is never translated (spec §1).
/// </summary>
public static class CommunityText
{
    private const string CategoryKeyPrefix = "Community.Category.";

    /// <summary>The Story categories the submit form offers. The stored value stays English; only the label is translated.</summary>
    public static IReadOnlyList<string> StoryCategories { get; } =
        ["Budgeting", "Debt", "Investing", "Savings", "Experiments", "Career", "Other"];

    /// <summary>Every category with a translated label: the Story categories plus the seeded poll categories.</summary>
    private static readonly HashSet<string> TranslatedCategories = new(StoryCategories.Append("Goals"), StringComparer.Ordinal);

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
    /// The label of a poll's or Story's category: translated when it is one the app defines (the Story form's or a
    /// seeded poll's), otherwise shown as stored.
    /// </summary>
    public static string Category(IStringLocalizer localizer, string category)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return TranslatedCategories.Contains(category)
            ? localizer[CategoryKeyPrefix + category]
            : category;
    }
}
