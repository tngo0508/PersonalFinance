using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// Service implementation for managing user saved articles (watchlist).
/// </summary>
internal sealed class WatchlistService : IWatchlistService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<WatchlistService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<SavedArticleResponseDto>> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default)
    {
        List<SavedArticleResponseDto> articles = await _dbContext.SavedArticles
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.SavedAtUtc)
            .Select(a => new SavedArticleResponseDto
            {
                Id = a.Id,
                Title = a.Title,
                Url = a.Url,
                Source = a.Source,
                Category = a.Category,
                Notes = a.Notes,
                ImageUrl = a.ImageUrl,
                SavedAtUtc = a.SavedAtUtc
            })
            .ToListAsync(cancellationToken);

        return articles;
    }

    /// <summary>
    /// Saves the article, or updates its notes when the Account already has it. An article the Account removed is
    /// restored rather than added again: (UserId, Url) is unique across removed rows too.
    /// </summary>
    public async Task<SaveArticleResultDto> SaveArticleAsync(string userId, SaveArticleRequestDto request, CancellationToken cancellationToken = default)
    {
        SavedArticle? existing = await _dbContext.SavedArticles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Url == request.Url, cancellationToken);

        if (existing is { IsDeleted: false })
        {
            existing.Notes = request.Notes ?? existing.Notes;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new SaveArticleResultDto { Success = true, AlreadySaved = true, Id = existing.Id };
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        SavedArticle article = existing ?? new SavedArticle { UserId = userId, Url = request.Url, CreatedAt = now };
        article.Title = request.Title;
        article.Source = request.Source;
        article.Category = request.Category;
        article.Notes = request.Notes;
        article.ImageUrl = request.ImageUrl;
        article.SavedAtUtc = now;

        if (existing is null)
        {
            _dbContext.SavedArticles.Add(article);
        }
        else
        {
            article.IsDeleted = false;
            article.DeletedAt = null;
            article.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} saved article {ArticleId}", userId, article.Id);
        }
        return new SaveArticleResultDto { Success = true, AlreadySaved = false, Id = article.Id };
    }

    public async Task<bool> RemoveArticleAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        SavedArticle? article = await _dbContext.SavedArticles
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (article is null) return false;

        article.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} removed article {ArticleId}", userId, id);
        }
        return true;
    }

    public async Task<bool> IsArticleSavedAsync(string userId, string url, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SavedArticles
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Url == url, cancellationToken);
    }
}
