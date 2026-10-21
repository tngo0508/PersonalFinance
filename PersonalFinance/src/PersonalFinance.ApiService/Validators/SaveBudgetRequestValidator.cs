using FluentValidation;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.ApiService.Validators;

/// <summary>
/// Validator for save budget requests. Ensures amounts are non-negative and valid.
/// </summary>
public class SaveBudgetRequestValidator : AbstractValidator<SaveBudgetRequestDto>
{
    public SaveBudgetRequestValidator()
    {
        RuleFor(x => x.MonthlyIncome)
            .GreaterThanOrEqualTo(0.01m)
            .WithMessage("Monthly income must be greater than 0.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Monthly income must not exceed 10,000,000.");

        RuleFor(x => x.Allocations.Housing)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Housing amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Housing amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Housing amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Food)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Food amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Food amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Food amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Transportation)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Transportation amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Transportation amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Transportation amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Utilities)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Utilities amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Utilities amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Utilities amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Insurance)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Insurance amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Insurance amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Insurance amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Healthcare)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Healthcare amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Healthcare amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Healthcare amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Savings)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Savings amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Savings amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Savings amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.DebtRepayment)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Debt repayment amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Debt repayment amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Debt repayment amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Entertainment)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Entertainment amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Entertainment amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Entertainment amount must have at most 2 decimal places.");

        RuleFor(x => x.Allocations.Miscellaneous)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Miscellaneous amount must be 0 or greater.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Miscellaneous amount must not exceed 10,000,000.")
            .Must(v => v == Math.Round(v, 2))
            .WithMessage("Miscellaneous amount must have at most 2 decimal places.");
    }
}
