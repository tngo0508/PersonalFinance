using PersonalFinance.Shared.DTOs.Watchlist;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the article Watchlist.
/// </summary>
public interface IWatchlistApi
{
    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [Get("/api/Watchlist")]
    Task<List<SavedArticleDto>> GetWatchlistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [Post("/api/Watchlist")]
    Task<SaveArticleResponseDto> SaveArticleAsync([Body] SaveArticleRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [Delete("/api/Watchlist/{id}")]
    Task<RemoveArticleResponseDto> RemoveArticleAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [Get("/api/Watchlist/check")]
    Task<CheckArticleSavedResponseDto> CheckArticleSavedAsync([Query] string url,
        CancellationToken cancellationToken = default);
}
