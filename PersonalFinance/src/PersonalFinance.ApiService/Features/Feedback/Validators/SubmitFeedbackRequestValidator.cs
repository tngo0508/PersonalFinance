using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Feedback.Validators;

/// <summary>
/// Validator for a request to send Feedback. <c>NotEmpty</c> also rejects a whitespace-only message, which would be
/// stored empty once trimmed. Every rule carries its <see cref="ApiErrorKeys.Feedback"/> key (ADR 0017).
/// </summary>
internal sealed class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
    public SubmitFeedbackRequestValidator()
    {
        RuleFor(x => x.Category)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.Feedback.CategoryUnknown)
                .WithMessage(FeedbackMessages.UnknownCategory);

        RuleFor(x => x.Message)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.Feedback.MessageRequired)
                .WithMessage(FeedbackMessages.MessageRequired)
            .MaximumLength(FeedbackLimits.MessageMaxLength)
                .WithErrorCode(ApiErrorKeys.Feedback.MessageLength)
                .WithMessage(FeedbackMessages.MessageTooLong)
                .WithState(_ => ErrorArgs.Number("max", FeedbackLimits.MessageMaxLength));

        RuleFor(x => x.PageUrl)
            .MaximumLength(FeedbackLimits.PageUrlMaxLength)
                .WithErrorCode(ApiErrorKeys.Feedback.PageTooLong)
                .WithMessage(FeedbackMessages.PageTooLong)
                .WithState(_ => ErrorArgs.Number("max", FeedbackLimits.PageUrlMaxLength))
            .Must(AppRelativePath.IsValid)
                .WithErrorCode(ApiErrorKeys.Feedback.PageNotAppRelative)
                .WithMessage(FeedbackMessages.PageNotAppRelative)
            .When(x => !string.IsNullOrEmpty(x.PageUrl));
    }
}
