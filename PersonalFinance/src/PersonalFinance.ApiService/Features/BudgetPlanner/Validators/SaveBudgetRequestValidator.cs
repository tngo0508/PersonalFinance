using System.Linq.Expressions;
using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.BudgetPlanner.Validators;

/// <summary>
/// Validator for save budget requests: the monthly income is over 0 and at most <see cref="BudgetPlannerLimits.MaxAmount"/>
/// scaled to the request's currency (<see cref="MoneyBounds"/>, #444); every category allocation is from 0 to that
/// bound in whole cents. Every rule carries its <see cref="ApiErrorKeys.BudgetPlanner"/> key and the bounds as money
/// arguments in that currency (ADR 0017).
/// </summary>
internal sealed class SaveBudgetRequestValidator : AbstractValidator<SaveBudgetRequestDto>
{
    private const string IncomeRange = "Monthly income must be over {min} and at most {max}.";
    private const string AllocationRange = "The amount for each category must be between {min} and {max}.";

    public SaveBudgetRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.BudgetPlanner.CurrencyUnsupported);

        RuleFor(x => x.MonthlyIncome)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange)
                .WithMessage(x => Describe(IncomeRange, x))
                .WithState(x => RangeArgs(x))
            .LessThanOrEqualTo(x => MaxAmount(x))
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange)
                .WithMessage(x => Describe(IncomeRange, x))
                .WithState(x => RangeArgs(x));

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
                .WithMessage(x => Describe(AllocationRange, x))
                .WithState(x => RangeArgs(x))
            .LessThanOrEqualTo(x => MaxAmount(x))
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.AllocationRange)
                .WithMessage(x => Describe(AllocationRange, x))
                .WithState(x => RangeArgs(x))
            .Must(value => value == Math.Round(value, 2))
                .WithErrorCode(ApiErrorKeys.BudgetPlanner.AllocationWholeCents)
                .WithMessage("The amount of each category can have at most two decimal places.");
    }

    /// <summary>The largest income or allocation in the request's currency: the dollar bound scaled.</summary>
    private static decimal MaxAmount(SaveBudgetRequestDto request) =>
        MoneyBounds.Max(BudgetPlannerLimits.MaxAmount, request.CurrencyCode);

    /// <summary>A range message's English text with the bounds in the request's currency.</summary>
    private static string Describe(string template, SaveBudgetRequestDto request) =>
        MoneyBounds.Describe(template, BudgetPlannerLimits.MaxAmount, request.CurrencyCode);

    /// <summary>The <c>{min}</c> and <c>{max}</c> of a range message, in the request's currency.</summary>
    private static IReadOnlyDictionary<string, ApiErrorArgDto> RangeArgs(SaveBudgetRequestDto request) =>
        ErrorArgs.MoneyRange(0m, MaxAmount(request), SupportedCurrencies.CodeOrDefault(request.CurrencyCode));
}
