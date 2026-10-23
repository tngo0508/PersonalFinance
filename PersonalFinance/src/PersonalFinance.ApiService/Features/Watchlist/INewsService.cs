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
}
