namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing an article saved by a user to their personal watchlist.
/// </summary>
public class SavedArticle : AuditableEntity
{
    /// <summary>Original article title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Original article URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Article source name (e.g., "Investopedia").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Article category (e.g., "Budgeting", "Investing").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Optional user notes on the article.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional image URL for the article thumbnail.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Timestamp when the article was saved.</summary>
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}
