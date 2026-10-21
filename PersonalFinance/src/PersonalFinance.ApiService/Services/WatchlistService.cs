using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation for managing user saved articles (watchlist).
/// </summary>
public class WatchlistService : IWatchlistService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        AppDbContext dbContext,
        ILogger<WatchlistService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<SavedArticleResponseDto>> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default)
    {
        var articles = await _dbContext.SavedArticles
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

    public async Task<SaveArticleResultDto> SaveArticleAsync(string userId, SaveArticleRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SavedArticles
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Url == request.Url, cancellationToken);

        if (existing is not null)
        {
            existing.Notes = request.Notes ?? existing.Notes;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new SaveArticleResultDto { Success = true, AlreadySaved = true, Id = existing.Id };
        }

        var article = new SavedArticle
        {
            UserId = userId,
            Title = request.Title,
            Url = request.Url,
            Source = request.Source,
            Category = request.Category,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl
        };

        _dbContext.SavedArticles.Add(article);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} saved article {ArticleId}", userId, article.Id);
        return new SaveArticleResultDto { Success = true, AlreadySaved = false, Id = article.Id };
    }

    public async Task<bool> RemoveArticleAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var article = await _dbContext.SavedArticles
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

        if (article is null) return false;

        article.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} removed article {ArticleId}", userId, id);
        return true;
    }

    public async Task<bool> IsArticleSavedAsync(string userId, string url, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SavedArticles
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Url == url, cancellationToken);
    }
}
