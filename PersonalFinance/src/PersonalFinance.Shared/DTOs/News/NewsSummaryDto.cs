using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Shared.DTOs.News;

/// <summary>
///     Request DTO for summarizing a news article.
/// </summary>
public class NewsArticleSummaryRequestDto
{
    /// <summary>
    ///     The title of the news article to summarize.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     The description/body text of the news article to summarize.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     The article's link. When it is an article of the news feed, the summary is written from the text the feed
    ///     provides for it (the article body where it can be read) rather than from <see cref="Description"/>.
    /// </summary>
    public string? Url { get; set; }
}

/// <summary>
///     Response DTO containing the AI-generated summary of a news article.
/// </summary>
public class NewsArticleSummaryResponseDto
{
    public string? BudgetWarning { get; set; }
    public string? Disclaimer { get; set; }

    /// <summary>
    ///     The summary as plain text: the TL;DR, the key points as bullets and why it matters, one paragraph each; or
    ///     the fixed message when <see cref="FixedText"/> is set.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The one-sentence TL;DR; null for a fixed server message.</summary>
    public string? Tldr { get; set; }

    /// <summary>Two to five short key points; empty when the model answered prose or for a fixed message.</summary>
    public List<string> KeyPoints { get; set; } = [];

    /// <summary>One sentence on what the news means for a household's money, with no advice; null when absent.</summary>
    public string? WhyItMatters { get; set; }

    /// <summary>
    ///     What the summary was written from, one of <see cref="NewsSummaryCoverage"/>; null for a fixed message.
    /// </summary>
    public string? Coverage { get; set; }

    /// <summary>
    ///     The keyed entry of <see cref="Summary"/> when it is a fixed server message rather than a model-written summary,
    ///     so the Web app can show it in the reader's language (#381); null for a model summary.
    /// </summary>
    public ApiErrorEntryDto? FixedText { get; set; }

    /// <summary>
    ///     The model used for the summary (e.g., "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp of the summary generation in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
