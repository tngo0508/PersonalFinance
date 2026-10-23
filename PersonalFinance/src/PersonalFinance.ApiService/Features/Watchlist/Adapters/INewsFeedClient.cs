using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// Reads one finance news source's RSS 2.0 feed.
/// </summary>
internal interface INewsFeedClient
{
    /// <summary>
    /// Returns up to five articles from the source, or none when the feed fails, times out or cannot be parsed, so
    /// that one failing source does not prevent the others from being read.
    /// </summary>
    Task<List<NewsArticleDto>> FetchAsync(NewsSourceDto source, CancellationToken cancellationToken);
}
