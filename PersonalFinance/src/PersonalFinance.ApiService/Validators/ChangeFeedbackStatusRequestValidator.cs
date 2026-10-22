using FluentValidation;
using PersonalFinance.Shared.DTOs.Feedback;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for an Admin's request to change a piece of Feedback's Feedback Status.
/// </summary>
public class ChangeFeedbackStatusRequestValidator : AbstractValidator<ChangeFeedbackStatusRequest>
{
    /// <summary>
    /// Shown when the requested status is not New, Reviewed or Archived.
    /// </summary>
    public const string UnknownStatusMessage = "Choose a status: New, Reviewed or Archived.";

    public ChangeFeedbackStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(UnknownStatusMessage);
    }
}
