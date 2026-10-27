using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AccountDeletion;

namespace PersonalFinance.ApiService.Features.AccountLifecycle.Validators;

/// <summary>
/// Validator for cancelling a Deletion Request: the token must look like one the service issued (Base64Url of 32
/// bytes) before it is hashed and looked up. A token that does not is one invalid link, so the first broken rule
/// stops the rest and every rule answers with the same key (ADR 0017).
/// </summary>
internal sealed class CancelDeletionRequestValidator : AbstractValidator<CancelDeletionRequest>
{
    public CancelDeletionRequestValidator()
    {
        RuleFor(x => x.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithErrorCode(ApiErrorKeys.AccountLifecycle.InvalidCancelLink)
                .WithMessage(AccountDeletionMessages.InvalidCancelLink)
            .MaximumLength(100)
                .WithErrorCode(ApiErrorKeys.AccountLifecycle.InvalidCancelLink)
                .WithMessage(AccountDeletionMessages.InvalidCancelLink)
            .Matches("^[A-Za-z0-9_-]+$")
                .WithErrorCode(ApiErrorKeys.AccountLifecycle.InvalidCancelLink)
                .WithMessage(AccountDeletionMessages.InvalidCancelLink);
    }
}
