namespace PersonalFinance.Shared.DTOs.Watchlist;

/// <summary>
/// One article from the finance news feed that articles are found in and saved to the Watchlist from.
/// </summary>
public class NewsArticleDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The article description with HTML stripped, truncated to 250 characters.</summary>
    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>The name of the news source (e.g., "CNN Business").</summary>
    public string Source { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public DateTime PublishedAt { get; set; }

    /// <summary>The article category (e.g., "General", "Investing", "Retirement").</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// A news source the feed reads, offered as a filter.
/// </summary>
public class NewsSourceDto
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Filters for the news feed. Every filter is optional and matched ignoring case.
/// </summary>
public class NewsQueryDto
{
    public string? Category { get; set; }
    public string? Source { get; set; }

    /// <summary>Free text matched against the title, description and source.</summary>
    public string? Search { get; set; }
}

/// <summary>
/// The news feed: the articles matching a <see cref="NewsQueryDto"/>, and the filter options. When the filters are
/// rejected, it is the body of the 400 response: no articles, the reasons in <see cref="Errors"/>, and the filter
/// options, so the page can still offer them.
/// </summary>
public class NewsFeedDto
{
    /// <summary>The articles matching the query, newest first.</summary>
    public List<NewsArticleDto> Articles { get; set; } = [];

    public List<NewsSourceDto> Sources { get; set; } = [];

    /// <summary>The topics some configured source covers, offered as the category filter, in source order.</summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>Why the filters were rejected; empty when they were applied.</summary>
    public List<string> Errors { get; set; } = [];
}
