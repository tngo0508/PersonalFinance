using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Feedback.Validators;

/// <summary>
/// Validator for a request to send Feedback. <c>NotEmpty</c> also rejects a whitespace-only message, which would be
/// stored empty once trimmed.
/// </summary>
internal sealed class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
    public SubmitFeedbackRequestValidator()
    {
        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage(FeedbackMessages.UnknownCategory);

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage(FeedbackMessages.MessageRequired)
            .MaximumLength(FeedbackLimits.MessageMaxLength)
            .WithMessage(FeedbackMessages.MessageTooLong);

        RuleFor(x => x.PageUrl)
            .MaximumLength(FeedbackLimits.PageUrlMaxLength)
            .WithMessage(FeedbackMessages.PageTooLong)
            .Must(AppRelativePath.IsValid)
            .WithMessage(FeedbackMessages.PageNotAppRelative)
            .When(x => !string.IsNullOrEmpty(x.PageUrl));
    }
}
