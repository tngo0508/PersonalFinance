using FluentValidation;
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
}
