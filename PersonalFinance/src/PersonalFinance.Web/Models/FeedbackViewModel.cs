using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models;

/// <summary>
/// The Send feedback form.
/// </summary>
public class FeedbackViewModel
{
    [EnumDataType(typeof(FeedbackCategory), ErrorMessage = "Choose a category: Bug, Idea or Other.")]
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Bug;

    [Required(ErrorMessage = "Message is required.")]
    [StringLength(FeedbackLimits.MessageMaxLength, ErrorMessage = "Message must be 2,000 characters or fewer.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The app-relative path of the page the Account came from, sent with the Feedback.
    /// </summary>
    public string? PageUrl { get; set; }
}
