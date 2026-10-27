using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for setting a planned amount. Every rule carries its <see cref="ApiErrorKeys.BudgetTracker"/> key and
/// the amount bounds as arguments (ADR 0017).
/// </summary>
internal sealed class SetPlannedRequestValidator : AbstractValidator<SetPlannedRequest>
{
    public const decimal MaxAmount = BudgetTrackerLimits.MaxAmount;

    public SetPlannedRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.BudgetTracker.CurrencyUnsupported);

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.PlannedAmountRange)
                .WithMessage("Planned amount must be greater than or equal to 0.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .LessThanOrEqualTo(MaxAmount)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.PlannedAmountRange)
                .WithMessage("Planned amount cannot exceed 9,999,999,999,999,999.99.")
                .WithState(_ => BudgetTrackerLimits.AmountRangeArgs)
            .Must(amount => decimal.Round(amount, 2) == amount)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.PlannedAmountWholeCents)
                .WithMessage("Planned amount cannot have more than 2 decimal places.");
    }
}
