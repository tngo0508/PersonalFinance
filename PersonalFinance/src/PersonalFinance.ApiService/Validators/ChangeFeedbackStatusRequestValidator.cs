using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for an Admin's request to change a piece of Feedback's Feedback Status.
/// </summary>
public class ChangeFeedbackStatusRequestValidator : AbstractValidator<ChangeFeedbackStatusRequest>
{
    public ChangeFeedbackStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(FeedbackMessages.UnknownStatus);
    }
}
