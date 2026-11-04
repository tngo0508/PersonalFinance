using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// Reads one finance news source's RSS 2.0 feed.
/// </summary>
internal interface INewsFeedClient
{
    /// <summary>
    /// Returns the source's five newest items, each with the longest text the feed carried for it, or none when the
    /// feed fails, times out or cannot be parsed, so that one failing source does not prevent the others from being
    /// read.
    /// </summary>
    Task<List<NewsFeedItem>> FetchAsync(NewsSourceDto source, CancellationToken cancellationToken);
}
