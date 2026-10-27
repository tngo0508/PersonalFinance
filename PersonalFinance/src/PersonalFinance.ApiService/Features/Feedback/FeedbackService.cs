using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// EF Core implementation of <see cref="IFeedbackService"/>.
/// </summary>
internal sealed class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _db;
    private readonly IRateLimiterService _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public FeedbackService(AppDbContext db, [FromKeyedServices(FeedbackRateLimit.Key)] IRateLimiterService rateLimiter,
        TimeProvider timeProvider)
    {
        _db = db;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The limiter slot is reserved before the save, atomically, so concurrent submissions from one Account cannot
    /// get past the limit. If the save fails, that slot is released so only stored Feedback counts. A cancelled
    /// request keeps its slot, because the cancellation may arrive after the database stored the Feedback.
    /// </remarks>
    public async Task<SubmitFeedbackResult> SubmitAsync(string userId, SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryReserve(userId, out DateTime slot))
        {
            return SubmitFeedbackResult.TooManyRequests();
        }

        _db.Feedback.Add(new FeedbackEntity
        {
            UserId = userId,
            Category = request.Category,
            Message = request.Message.Trim(),
            PageUrl = request.PageUrl
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _rateLimiter.Release(userId, slot);
            throw;
        }

        return SubmitFeedbackResult.Success();
    }

    /// <inheritdoc />
    public Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
        CancellationToken cancellationToken = default) =>
        AdminListQuery(status).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ChangeStatusAsync(int id, FeedbackStatus status,
        CancellationToken cancellationToken = default)
    {
        FeedbackEntity? feedback = await _db.Feedback.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (feedback is null)
        {
            return false;
        }

        feedback.Status = status;
        feedback.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The Admin inbox query: one round trip, newest first with the id as tie-breaker, soft-deleted Feedback
    /// excluded by the global query filter. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<FeedbackAdminItemDto> AdminListQuery(FeedbackStatus? status = null) =>
        from feedback in _db.Feedback.AsNoTracking()
        where status == null ? feedback.Status != FeedbackStatus.Archived : feedback.Status == status
        join user in _db.Users.AsNoTracking() on feedback.UserId equals user.Id into users
        from user in users.DefaultIfEmpty()
        orderby feedback.CreatedAt descending, feedback.Id descending
        select new FeedbackAdminItemDto
        {
            Id = feedback.Id,
            Category = feedback.Category,
            Message = feedback.Message,
            AccountEmail = user == null ? string.Empty : user.Email ?? user.UserName ?? string.Empty,
            PageUrl = feedback.PageUrl,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt
        };
}
