namespace PersonalFinance.Shared.DTOs;

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
    /// <summary>
    ///     The AI-generated summary text.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    ///     The model used for the summary (e.g., "gpt-4o-mini").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    ///     Timestamp of the summary generation in UTC.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}