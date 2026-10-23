using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for setting a planned amount.
/// </summary>
internal sealed class SetPlannedRequestValidator : AbstractValidator<SetPlannedRequest>
{
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public SetPlannedRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Planned amount must be greater than or equal to 0.")
            .LessThanOrEqualTo(MaxAmount)
            .WithMessage("Planned amount cannot exceed 9,999,999,999,999,999.99.")
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Planned amount cannot have more than 2 decimal places.");
    }
}
