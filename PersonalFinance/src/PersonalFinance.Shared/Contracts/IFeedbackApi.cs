using PersonalFinance.Shared.DTOs.Feedback;
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
    /// Lists all Feedback for the Admin inbox, newest first. Requires the Admin Policy.
    /// </summary>
    [Get("/api/feedback")]
    Task<List<FeedbackAdminItemDto>> ListAsync(CancellationToken cancellationToken = default);
}
