using Microsoft.AspNetCore.Razor.TagHelpers;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Web.TagHelpers;

/// <summary>
/// TagHelper that renders a human-friendly relative time string (e.g., "5m ago", "2h ago")
/// for any element containing a <c>datetime</c> attribute with an ISO 8601 value.
/// Auto-applies to all &lt;time&gt; elements project-wide.
/// </summary>
[HtmlTargetElement("time", Attributes = DatetimeAttributeName)]
public class TimeAgoTagHelper : TagHelper
{
    private const string DatetimeAttributeName = "datetime";

    /// <summary>
    /// Gets or sets the ISO 8601 datetime value from the element's <c>datetime</c> attribute.
    /// </summary>
    [HtmlAttributeName(DatetimeAttributeName)]
    public string? Datetime { get; set; }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrEmpty(Datetime) ||
            !DateTime.TryParse(Datetime, out var parsedDate))
        {
            return;
        }

        var relativeTime = TimeAgoText(parsedDate);
        // Prepend the relative time text while preserving any child elements (e.g., SVG icons)
        var existingContent = output.Content.GetContent();
        output.Content.SetContent($"{relativeTime}{existingContent}");
    }

    /// <summary>
    /// Converts a UTC DateTime to a human-readable relative time string.
    /// Used by both the TagHelper and views that need inline relative-time formatting.
    /// </summary>
    public static string TimeAgoText(DateTime dateTime)
    {
        // Ensure the comparison is in UTC
        var utcNow = DateTime.UtcNow;
        var input = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        var span = utcNow - input;

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
