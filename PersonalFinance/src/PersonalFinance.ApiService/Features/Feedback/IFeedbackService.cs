using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Feedback;

/// <summary>
/// Owns Feedback: Accounts send it, Admins read and triage it.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Stores Feedback for <paramref name="userId"/> with status New, unless the Account has hit the submission
    /// limit. The caller validates <paramref name="request"/> first, so invalid requests never count toward it.
    /// </summary>
    Task<SubmitFeedbackResult> SubmitAsync(string userId, SubmitFeedbackRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Feedback for the Admin inbox, newest first, with the sending Account's email. With a
    /// <paramref name="status"/>, only Feedback with that Feedback Status; without one, New and Reviewed
    /// Feedback (Archived is hidden unless asked for).
    /// </summary>
    Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the Feedback Status of the Feedback with <paramref name="id"/>. Returns false when no such
    /// Feedback exists (soft-deleted Feedback counts as not existing).
    /// </summary>
    Task<bool> ChangeStatusAsync(int id, FeedbackStatus status, CancellationToken cancellationToken = default);
}
