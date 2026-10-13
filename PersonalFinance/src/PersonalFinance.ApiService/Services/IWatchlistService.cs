using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for managing user saved articles (watchlist).
/// </summary>
public interface IWatchlistService
{
    /// <summary>
    /// Gets all saved articles for a user.
    /// </summary>
    Task<List<SavedArticleResponseDto>> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    Task<SaveArticleResultDto> SaveArticleAsync(string userId, SaveArticleRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    Task<bool> RemoveArticleAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has saved a specific article URL.
    /// </summary>
    Task<bool> IsArticleSavedAsync(string userId, string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response DTO for a saved article.
/// </summary>
public sealed class SavedArticleResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SavedAtUtc { get; set; }
}

/// <summary>
/// Result of saving an article.
/// </summary>
public sealed class SaveArticleResultDto
{
    public bool Success { get; set; }
    public bool AlreadySaved { get; set; }
    public int Id { get; set; }
}
