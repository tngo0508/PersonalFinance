using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AccountDeletion;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for cancelling a Deletion Request: the token must look like one the service issued (Base64Url of 32
/// bytes) before it is hashed and looked up.
/// </summary>
public class CancelDeletionRequestValidator : AbstractValidator<CancelDeletionRequest>
{
    public CancelDeletionRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage(AccountDeletionMessages.InvalidCancelLink);
    }
}
