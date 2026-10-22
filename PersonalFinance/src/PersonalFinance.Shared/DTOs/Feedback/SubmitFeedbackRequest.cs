using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Feedback;

/// <summary>
/// Request body for sending Feedback as the signed-in Account.
/// </summary>
public class SubmitFeedbackRequest
{
    public FeedbackCategory Category { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The app-relative path of the page the Account came from (for example <c>/Dashboard</c>), if known.
    /// </summary>
    public string? PageUrl { get; set; }
}
