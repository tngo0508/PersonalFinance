namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the Finance News page.
/// </summary>
public class NewsViewModel
{
    /// <summary>
    /// List of news articles from all sources.
    /// </summary>
    public List<Services.NewsArticle> Articles { get; set; } = new();

    /// <summary>
    /// Available news sources for filtering.
    /// </summary>
    public List<Services.NewsSource> Sources { get; set; } = new();

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Currently selected category filter.
    /// </summary>
    public string? SelectedCategory { get; set; }

    /// <summary>
    /// Currently selected source filter.
    /// </summary>
    public string? SelectedSource { get; set; }

    /// <summary>
    /// Search query for filtering articles by title/description.
    /// </summary>
    public string? SearchQuery { get; set; }

    /// <summary>
    /// User-facing error message if feeds fail to load.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the data is still loading.
    /// </summary>
    public bool IsLoading { get; set; } = true;

    /// <summary>
    /// Last refresh timestamp.
    /// </summary>
    public DateTime? LastRefreshed { get; set; }
}
