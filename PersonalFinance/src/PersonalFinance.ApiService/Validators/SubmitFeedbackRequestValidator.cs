using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for a request to send Feedback.
/// </summary>
public class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
    public SubmitFeedbackRequestValidator()
    {
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Choose a category: Bug, Idea or Other.");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required.")
            .MaximumLength(FeedbackLimits.MessageMaxLength)
            .WithMessage("Message must be 2,000 characters or fewer.");

        RuleFor(x => x.PageUrl)
            .MaximumLength(FeedbackLimits.PageUrlMaxLength)
            .WithMessage("Page must be 500 characters or fewer.")
            .Must(AppRelativePath.IsValid)
            .WithMessage("Page must be a path within the app.")
            .When(x => !string.IsNullOrEmpty(x.PageUrl));
    }
}
