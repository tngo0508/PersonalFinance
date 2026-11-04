using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// The finance news feed that articles are found in and saved to the Watchlist from.
/// </summary>
public interface INewsService
{
    /// <summary>
    /// Gets the articles matching the query, newest first, with the filter options. The fetched feed is cached for a
    /// short time and shared across requests.
    /// </summary>
    Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the filter options (the configured sources and the topics they cover) with no articles, for a query whose
    /// filters were rejected.
    /// </summary>
    Task<NewsFeedDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the text an AI summary of the article at <paramref name="url"/> is written from: the article body when the
    /// feed carried it or its page can be read, else the feed's excerpt, else the headline alone. Null when the URL is
    /// not an article of the feed, so no page is ever fetched on a caller's say-so.
    /// </summary>
    Task<NewsArticleText?> GetArticleTextAsync(string url, CancellationToken cancellationToken = default);
}
