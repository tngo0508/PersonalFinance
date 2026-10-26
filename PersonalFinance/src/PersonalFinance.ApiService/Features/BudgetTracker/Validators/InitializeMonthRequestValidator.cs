using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker.Validators;

/// <summary>
/// Validator for month initialization requests. Every rule carries its <see cref="ApiErrorKeys.BudgetTracker"/> key
/// and the bounds as arguments (ADR 0017).
/// </summary>
internal sealed class InitializeMonthRequestValidator : AbstractValidator<InitializeMonthRequest>
{
    public InitializeMonthRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(BudgetTrackerLimits.MinYear, BudgetTrackerLimits.MaxYear)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.YearRange)
                .WithMessage("Year must be between 1900 and 2100.")
                .WithState(_ => BudgetTrackerLimits.YearRangeArgs);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
                .WithErrorCode(ApiErrorKeys.BudgetTracker.MonthRange)
                .WithMessage("Month must be between 1 and 12.")
                .WithState(_ => BudgetTrackerLimits.MonthRangeArgs);
    }
}
