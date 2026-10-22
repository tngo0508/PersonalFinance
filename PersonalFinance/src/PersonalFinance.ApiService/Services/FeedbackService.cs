using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// EF Core implementation of <see cref="IFeedbackService"/>.
/// </summary>
public class FeedbackService : IFeedbackService
{
    private readonly AppDbContext _db;
    private readonly IValidator<SubmitFeedbackRequest> _validator;

    public FeedbackService(AppDbContext db, IValidator<SubmitFeedbackRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<SubmitFeedbackResult> SubmitAsync(string userId, SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return SubmitFeedbackResult.Invalid(validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        _db.Feedback.Add(new FeedbackEntity
        {
            UserId = userId,
            Category = request.Category,
            Message = request.Message.Trim(),
            PageUrl = request.PageUrl
        });
        await _db.SaveChangesAsync(cancellationToken);
        return SubmitFeedbackResult.Success();
    }

    /// <inheritdoc />
    public Task<List<FeedbackAdminItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
        AdminListQuery().ToListAsync(cancellationToken);

    /// <summary>
    /// The Admin inbox query: one round trip, newest first with the id as tie-breaker, soft-deleted Feedback
    /// excluded by the global query filter. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<FeedbackAdminItemDto> AdminListQuery() =>
        from feedback in _db.Feedback.AsNoTracking()
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
