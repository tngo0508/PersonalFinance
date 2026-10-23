using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for setting or clearing a manual Actual override.
/// </summary>
internal sealed class SetActualOverrideRequestValidator : AbstractValidator<SetActualOverrideRequest>
{
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public SetActualOverrideRequestValidator()
    {
        RuleFor(x => x.Amount)
            .Must(amount => !amount.HasValue || amount.Value >= 0)
            .WithMessage("Actual override must be greater than or equal to 0 when specified.")
            .Must(amount => !amount.HasValue || amount.Value <= MaxAmount)
            .WithMessage("Actual override cannot exceed 9,999,999,999,999,999.99.")
            .Must(amount => !amount.HasValue || decimal.Round(amount.Value, 2) == amount.Value)
            .WithMessage("Actual override cannot have more than 2 decimal places.");
    }
}
