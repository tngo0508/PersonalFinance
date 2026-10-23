using PersonalFinance.Shared.DTOs.Watchlist;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the finance news feed of the Watchlist Feature.
/// </summary>
public interface INewsApi
{
    /// <summary>
    /// Gets the news articles matching the query, with the filter options.
    /// </summary>
    [Get("/api/News")]
    Task<NewsFeedDto> GetNewsAsync([Query] NewsQueryDto query, CancellationToken cancellationToken = default);
}
