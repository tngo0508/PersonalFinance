using Microsoft.Extensions.Localization;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// The Guild Hall page's own wording around user content, in the request's Language. User content itself (titles,
/// bodies, comments, Pseudonyms, poll questions) is never translated (spec §1).
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

    /// <summary>
    /// Who a post is shown as written by: the author's Pseudonym, or the translated "Former member" when the author
    /// has none any more because the Account was purged (ADR 0005, ADR 0018).
    /// </summary>
    public static string Author(IStringLocalizer localizer, string? pseudonym)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return string.IsNullOrWhiteSpace(pseudonym) ? localizer["Community.FormerMember"] : pseudonym;
    }

    /// <summary>
    /// The avatar letters of an author label: the first letter of up to <paramref name="count"/> words, upper-cased
    /// ("Steady Heron" gives "SH").
    /// </summary>
    public static string Initials(string author, int count)
    {
        ArgumentNullException.ThrowIfNull(author);
        IEnumerable<char> letters = author
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(count)
            .Select(word => char.ToUpperInvariant(word[0]));
        return string.Concat(letters);
    }
}
