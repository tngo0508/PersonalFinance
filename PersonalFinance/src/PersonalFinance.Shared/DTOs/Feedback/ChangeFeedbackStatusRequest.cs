using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Feedback;

/// <summary>
/// Request body for an Admin setting the Feedback Status of one piece of Feedback.
/// </summary>
public class ChangeFeedbackStatusRequest
{
    public FeedbackStatus Status { get; set; }
}
