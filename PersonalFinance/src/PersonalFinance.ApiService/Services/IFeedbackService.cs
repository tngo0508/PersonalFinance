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
}
