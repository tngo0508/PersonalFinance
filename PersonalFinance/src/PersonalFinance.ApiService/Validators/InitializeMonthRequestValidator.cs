using FluentValidation;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for month initialization requests.
/// </summary>
public class InitializeMonthRequestValidator : AbstractValidator<InitializeMonthRequest>
{
    public InitializeMonthRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(1900, 2100)
            .WithMessage("Year must be between 1900 and 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be between 1 and 12.");
    }
}
