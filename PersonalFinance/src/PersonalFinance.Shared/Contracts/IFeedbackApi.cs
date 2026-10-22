using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Feedback.
/// </summary>
public interface IFeedbackApi
{
    /// <summary>
    /// Sends Feedback as the signed-in Account.
    /// </summary>
    [Post("/api/feedback")]
    Task SubmitAsync([Body] SubmitFeedbackRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Feedback for the Admin inbox, newest first. With <paramref name="status"/>, only Feedback with that
    /// Feedback Status; without one, Archived Feedback is hidden. Requires the Admin Policy.
    /// </summary>
    [Get("/api/feedback")]
    Task<List<FeedbackAdminItemDto>> ListAsync([Query] FeedbackStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the Feedback Status of one piece of Feedback. Requires the Admin Policy; unknown Feedback is 404.
    /// </summary>
    [Put("/api/feedback/{id}/status")]
    Task ChangeStatusAsync(int id, [Body] ChangeFeedbackStatusRequest request,
        CancellationToken cancellationToken = default);
}
