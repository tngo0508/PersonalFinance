using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.TagHelpers;

/// <summary>
/// TagHelper that renders a human-friendly relative time string (e.g., "5m ago", "2h ago")
/// for any element containing a <c>datetime</c> attribute with an ISO 8601 value.
/// Auto-applies to all &lt;time&gt; elements project-wide. The text is in the request's Language
/// (<c>Common.TimeAgo.*</c>); a date older than a week uses that Language's date format and the request culture.
/// </summary>
[HtmlTargetElement("time", Attributes = DatetimeAttributeName)]
public class TimeAgoTagHelper : TagHelper
{
    private const string DatetimeAttributeName = "datetime";

    private readonly IStringLocalizer _localizer;

    public TimeAgoTagHelper(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Gets or sets the ISO 8601 datetime value from the element's <c>datetime</c> attribute.
    /// </summary>
    [HtmlAttributeName(DatetimeAttributeName)]
    public string? Datetime { get; set; }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrEmpty(Datetime) ||
            !DateTime.TryParse(Datetime, out DateTime parsedDate))
        {
            return;
        }

        string relativeTime = TimeAgoText(parsedDate, _localizer);
        // Prepend the relative time text while preserving any child elements (e.g., SVG icons)
        string existingContent = output.Content.GetContent();
        output.Content.SetContent($"{relativeTime}{existingContent}");
    }

    /// <summary>
    /// Converts a UTC DateTime to a human-readable relative time string.
    /// Used by both the TagHelper and views that need inline relative-time formatting. Without a
    /// <paramref name="localizer"/> the text is English, as before localization.
    /// </summary>
    public static string TimeAgoText(DateTime dateTime, IStringLocalizer? localizer = null)
    {
        // Ensure the comparison is in UTC
        DateTime utcNow = DateTime.UtcNow;
        DateTime input = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        TimeSpan span = utcNow - input;

        if (localizer is null)
        {
            return EnglishTimeAgo(input, span);
        }

        if (span.TotalMinutes < 1)
            return localizer["Common.TimeAgo.JustNow"];

        if (span.TotalMinutes < 60)
            return localizer.Named("Common.TimeAgo.Minutes", ("count", (int)span.TotalMinutes));

        if (span.TotalHours < 24)
            return localizer.Named("Common.TimeAgo.Hours", ("count", (int)span.TotalHours));

        if (span.TotalDays < 7)
            return localizer.Named("Common.TimeAgo.Days", ("count", (int)span.TotalDays));

        return input.ToString(localizer["Common.TimeAgo.DateFormat"], CultureInfo.CurrentCulture);
    }

    private static string EnglishTimeAgo(DateTime input, TimeSpan span)
    {
        if (span.TotalMinutes < 1)
            return "Just now";

        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m ago";

        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours}h ago";

        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays}d ago";

        return input.ToString("MMM dd, yyyy", AppCulture.Default);
    }
}
