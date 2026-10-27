using System.Linq.Expressions;
using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.ApiService.Features.BudgetPlanner.Validators;

/// <summary>
/// Validator for save budget requests: the monthly income is over 0 and at most <see cref="MaxAmount"/>; every
/// category allocation is from 0 to <see cref="MaxAmount"/> in whole cents. Every rule carries its
/// <see cref="ApiErrorKeys.BudgetPlanner"/> key and the bounds as arguments (ADR 0017).
/// </summary>
internal sealed class SaveBudgetRequestValidator : AbstractValidator<SaveBudgetRequestDto>
{
    /// <summary>The largest monthly income or category allocation.</summary>
    public const decimal MaxAmount = 10_000_000m;

    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> AmountRangeArgs =
        ErrorArgs.MoneyRange(0m, MaxAmount, SupportedCurrencies.Default.Code);

    public SaveBudgetRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.BudgetPlanner.CurrencyUnsupported);

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange)
                .WithMessage("Monthly income must be over $0 and at most $10,000,000.")
                .WithState(_ => AmountRangeArgs)
            .LessThanOrEqualTo(MaxAmount)
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange)
                .WithMessage("Monthly income must be over $0 and at most $10,000,000.")
                .WithState(_ => AmountRangeArgs);

        AllocationRules(x => x.Allocations.Housing);
        AllocationRules(x => x.Allocations.Food);
        AllocationRules(x => x.Allocations.Transportation);
        AllocationRules(x => x.Allocations.Utilities);
        AllocationRules(x => x.Allocations.Insurance);
        AllocationRules(x => x.Allocations.Healthcare);
        AllocationRules(x => x.Allocations.Savings);
        AllocationRules(x => x.Allocations.DebtRepayment);
        AllocationRules(x => x.Allocations.Entertainment);
        AllocationRules(x => x.Allocations.Miscellaneous);
    }

    /// <summary>The range and whole-cents rules every category allocation shares; the failure names the field.</summary>
    private void AllocationRules(Expression<Func<SaveBudgetRequestDto, decimal>> allocation)
    {
        RuleFor(allocation)
            .GreaterThanOrEqualTo(0m)
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.AllocationRange)
                .WithMessage("The amount for each category must be between $0 and $10,000,000.")
                .WithState(_ => AmountRangeArgs)
            .LessThanOrEqualTo(MaxAmount)
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.AllocationRange)
                .WithMessage("The amount for each category must be between $0 and $10,000,000.")
                .WithState(_ => AmountRangeArgs)
            .Must(value => value == Math.Round(value, 2))
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.AllocationWholeCents)
                .WithMessage("The amount of each category can have at most two decimal places.");
    }
}
