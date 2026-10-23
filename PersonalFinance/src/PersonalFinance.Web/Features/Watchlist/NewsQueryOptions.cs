namespace PersonalFinance.Web.Features.Watchlist;

/// <summary>
/// Represents filter criteria for querying finance news articles.
/// </summary>
public sealed class NewsQueryOptions
{
    /// <summary>
    /// Gets or sets the category filter (e.g., "General", "Markets", "Finance").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the source name filter (e.g., "CNN Business", "Forbes Finance").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the free-text search query applied to title, description, and source.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Returns true if any filter criteria is specified.
    /// </summary>
    public bool HasFilters =>
        !string.IsNullOrEmpty(Category) ||
        !string.IsNullOrEmpty(Source) ||
        !string.IsNullOrEmpty(Search);
}
