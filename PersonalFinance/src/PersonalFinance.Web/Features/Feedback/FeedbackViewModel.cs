using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Features.Feedback;

/// <summary>
/// The Send feedback form.
/// </summary>
public class FeedbackViewModel
{
    [EnumDataType(typeof(FeedbackCategory), ErrorMessage = FeedbackMessages.UnknownCategory)]
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Bug;

    [Required(ErrorMessage = FeedbackMessages.MessageRequired)]
    [StringLength(FeedbackLimits.MessageMaxLength, ErrorMessage = FeedbackMessages.MessageTooLongFormat)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The app-relative path of the page the Account came from, sent with the Feedback.
    /// </summary>
    public string? PageUrl { get; set; }
}
