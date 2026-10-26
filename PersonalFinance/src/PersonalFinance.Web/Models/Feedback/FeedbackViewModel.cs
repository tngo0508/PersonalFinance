using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Feedback;

/// <summary>
/// The Send feedback form. Labels and validation messages are keys in <c>Resources/i18n/en.json</c>; the
/// ApiService validators keep their own English <see cref="FeedbackMessages"/>.
/// </summary>
public class FeedbackViewModel
{
    [Display(Name = "Feedback.Form.Category")]
    [EnumDataType(typeof(FeedbackCategory), ErrorMessage = "Feedback.Validation.CategoryUnknown")]
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Bug;

    [Display(Name = "Feedback.Form.Message")]
    [Required(ErrorMessage = "Feedback.Validation.MessageRequired")]
    [StringLength(FeedbackLimits.MessageMaxLength, ErrorMessage = "Feedback.Validation.MessageTooLong")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The app-relative path of the page the Account came from, sent with the Feedback.
    /// </summary>
    public string? PageUrl { get; set; }
}
