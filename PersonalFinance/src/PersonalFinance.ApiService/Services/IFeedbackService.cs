using PersonalFinance.Shared.DTOs.Feedback;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Owns Feedback: Accounts send it, Admins read and triage it.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Validates and stores Feedback for <paramref name="userId"/> with status New.
    /// </summary>
    Task<SubmitFeedbackResult> SubmitAsync(string userId, SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all Feedback for the Admin inbox, newest first, with the sending Account's email.
    /// </summary>
    Task<List<FeedbackAdminItemDto>> ListAsync(CancellationToken cancellationToken = default);
}
