using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;

namespace PersonalFinance.ApiService.Features.Feedback.Validators;

/// <summary>
/// Validator for an Admin's request to change a piece of Feedback's Feedback Status.
/// </summary>
internal sealed class ChangeFeedbackStatusRequestValidator : AbstractValidator<ChangeFeedbackStatusRequest>
{
    public ChangeFeedbackStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(FeedbackMessages.UnknownStatus);
    }
}
