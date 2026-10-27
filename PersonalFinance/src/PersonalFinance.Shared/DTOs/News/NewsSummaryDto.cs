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
}

/// <summary>
///     Response DTO containing the AI-generated summary of a news article.
/// </summary>
public class NewsArticleSummaryResponseDto
{
    public string? BudgetWarning { get; set; }
    public string? Disclaimer { get; set; }

    /// <summary>
    ///     The AI-generated summary text.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

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
