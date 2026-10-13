namespace PersonalFinance.Web.Models;

/// <summary>
/// Response model for the news refresh API endpoint.
/// </summary>
public sealed class NewsRefreshResponse
{
    /// <summary>
    /// Gets or sets whether the refresh operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of filtered news articles.
    /// </summary>
    public List<Services.NewsArticle>? Articles { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the data was last refreshed.
    /// </summary>
    public DateTime? LastRefreshed { get; set; }

    /// <summary>
    /// Gets or sets an error message when the refresh operation fails.
    /// </summary>
    public string? Error { get; set; }
}
