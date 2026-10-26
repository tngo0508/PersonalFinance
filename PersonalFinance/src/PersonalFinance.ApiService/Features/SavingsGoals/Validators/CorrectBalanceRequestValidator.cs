using FluentValidation;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals.Validators;

/// <summary>
/// Validator for a balance correction: the new saved amount is zero or more.
/// </summary>
internal sealed class CorrectBalanceRequestValidator : AbstractValidator<CorrectBalanceRequestDto>
{
    public CorrectBalanceRequestValidator()
    {
        RuleFor(x => x.Balance)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Balance cannot be negative.")
            .LessThanOrEqualTo(ContributionRules.MaxAmount)
            .WithMessage("Balance cannot exceed 9,999,999,999,999,999.99.")
            .Must(balance => decimal.Round(balance, 2) == balance)
            .WithMessage("Balance cannot have more than 2 decimal places.");
    }
}
