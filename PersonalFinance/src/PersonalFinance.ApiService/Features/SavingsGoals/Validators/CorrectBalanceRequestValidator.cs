using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for a balance correction: the new saved amount is zero or more. Every rule carries its
/// <see cref="ApiErrorKeys.SavingsGoals"/> key (ADR 0017).
/// </summary>
internal sealed class CorrectBalanceRequestValidator : AbstractValidator<CorrectBalanceRequestDto>
{
    public CorrectBalanceRequestValidator()
    {
        RuleFor(x => x.Balance)
            .GreaterThanOrEqualTo(0m)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.BalanceNegative)
                .WithMessage("Balance cannot be negative.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.BalanceTooLarge)
                .WithMessage("Balance cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => ContributionRules.MaxAmountArgs)
            .Must(balance => decimal.Round(balance, 2) == balance)
                .WithErrorCode(ApiErrorKeys.SavingsGoals.BalanceWholeCents)
                .WithMessage("Balance cannot have more than 2 decimal places.");
    }
}
