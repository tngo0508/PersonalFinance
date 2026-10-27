using FluentValidation;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.ApiService.Features.Feedback.Validators;

/// <summary>
/// Validator for the Admin Feedback inbox's status filter. No status is valid; the failure names the
/// <c>status</c> query parameter, the field the client sent.
/// </summary>
internal sealed class FeedbackListFilterValidator : AbstractValidator<FeedbackListFilter>
{
    public FeedbackListFilterValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.Feedback.StatusUnknown)
                .WithMessage(FeedbackMessages.UnknownStatus)
            .OverridePropertyName("status");
    }
}
