using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for setting or clearing a manual Actual override. Every rule carries its
/// <see cref="ApiErrorKeys.BudgetTracker"/> key and the amount bounds as arguments (ADR 0017).
/// </summary>
internal sealed class SetActualOverrideRequestValidator : AbstractValidator<SetActualOverrideRequest>
{
    public const decimal MaxAmount = BudgetTrackerLimits.MaxAmount;

    public SetActualOverrideRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.BudgetTracker.CurrencyUnsupported);

        RuleFor(x => x.Amount)
            .Must(amount => !amount.HasValue || amount.Value >= 0)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.ActualOverrideRange)
                .WithMessage("Actual override must be greater than or equal to 0 when specified.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .Must(amount => !amount.HasValue || amount.Value <= MaxAmount)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.ActualOverrideRange)
                .WithMessage("Actual override cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .Must(amount => !amount.HasValue || decimal.Round(amount.Value, 2) == amount.Value)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.ActualOverrideWholeCents)
                .WithMessage("Actual override cannot have more than 2 decimal places.");
    }
}
