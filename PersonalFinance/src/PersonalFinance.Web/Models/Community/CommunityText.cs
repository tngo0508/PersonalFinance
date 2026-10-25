using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// The Community page's own wording around user content, in the request's Language. User content itself (titles,
/// bodies, comments, author names, poll questions) is never translated (spec §1).
/// </summary>
public static class CommunityText
{
    private const string StatKeyPrefix = "Community.Stats.";
    private const string CategoryKeyPrefix = "Community.Category.";

    /// <summary>The Story categories the submit form offers. The stored value stays English; only the label is translated.</summary>
    public static IReadOnlyList<string> StoryCategories { get; } =
        ["Budgeting", "Debt", "Investing", "Savings", "Experiments", "Career", "Other"];

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

    /// <summary>The label of a Story's category: translated when it is one the form offers, otherwise shown as stored.</summary>
    public static string StoryCategory(IStringLocalizer localizer, string category)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return StoryCategories.Contains(category, StringComparer.Ordinal)
            ? localizer[CategoryKeyPrefix + category]
            : category;
    }

    /// <summary>A quick stat's title, by its key; the API's English title when the key is not one this page knows.</summary>
    public static string StatTitle(IStringLocalizer localizer, CommunityStat stat)
    {
        ArgumentNullException.ThrowIfNull(stat);
        return StatText(localizer, stat.Key, "Title", stat.Title);
    }

    /// <summary>A quick stat's description, by its key; the API's English text when the key is not one this page knows.</summary>
    public static string StatDescription(IStringLocalizer localizer, CommunityStat stat)
    {
        ArgumentNullException.ThrowIfNull(stat);
        return StatText(localizer, stat.Key, "Description", stat.Description);
    }

    private static string StatText(IStringLocalizer localizer, string key, string part, string fallback)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        if (string.IsNullOrEmpty(key))
        {
            return fallback;
        }

        LocalizedString text = localizer[$"{StatKeyPrefix}{key}.{part}"];
        return text.ResourceNotFound ? fallback : text.Value;
    }
}
