using System.Globalization;
using System.Linq.Expressions;
using FluentValidation;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios.Validators;

/// <summary>
/// Validator for saving an Investment Scenario: the Investment Calculator's input bounds (#587,
/// <see cref="InvestmentCalculatorLimits"/>), with the money bounds scaled to the scenario's currency
/// (<see cref="MoneyBounds"/>) and carried as money arguments; the scale rules match the column precision, so a value
/// round-trips unchanged. A Goal scenario needs a goal amount and a question; a Grow scenario's goal is ignored (the
/// service stores none). Every rule carries its <see cref="ApiErrorKeys.InvestmentScenarios"/> key (ADR 0017).
/// </summary>
internal sealed class SaveInvestmentScenarioRequestValidator : AbstractValidator<SaveInvestmentScenarioRequestDto>
{
    public SaveInvestmentScenarioRequestValidator()
    {
        RuleFor(x => x.Name).ScenarioName();

        RuleFor(x => x.Note)
            .MaximumLength(InvestmentCalculatorLimits.ScenarioNoteMaxLength)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.NoteTooLong)
                .WithMessage($"The note can contain up to {InvestmentCalculatorLimits.ScenarioNoteMaxLength} characters.")
                .WithState(_ => ErrorArgs.Number("max", InvestmentCalculatorLimits.ScenarioNoteMaxLength));

        RuleFor(x => x.Mode)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.ModeInvalid)
                .WithMessage("Choose Grow or Goal.");

        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.InvestmentScenarios.CurrencyUnsupported);

        Amount(x => x.InitialAmount, InvestmentCalculatorLimits.InitialMax,
            ApiErrorKeys.InvestmentScenarios.InitialAmountRange, "Starting amount");
        Amount(x => x.MonthlyContribution, InvestmentCalculatorLimits.MonthlyMax,
            ApiErrorKeys.InvestmentScenarios.MonthlyContributionRange, "Monthly contribution");

        Rate(x => x.AssumedReturn, InvestmentCalculatorLimits.AssumedReturnMax,
            ApiErrorKeys.InvestmentScenarios.AssumedReturnRange, "Assumed return");
        Rate(x => x.ContributionIncrease, InvestmentCalculatorLimits.ContributionIncreaseMax,
            ApiErrorKeys.InvestmentScenarios.ContributionIncreaseRange, "Yearly raise");
        Rate(x => x.Inflation, InvestmentCalculatorLimits.InflationMax,
            ApiErrorKeys.InvestmentScenarios.InflationRange, "Inflation");
        Rate(x => x.Fee, InvestmentCalculatorLimits.FeeMax, ApiErrorKeys.InvestmentScenarios.FeeRange, "Fee");

        RuleFor(x => x.Years)
            .InclusiveBetween(InvestmentCalculatorLimits.YearsMin, InvestmentCalculatorLimits.YearsMax)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.YearsRange)
                .WithMessage($"Years must be between {InvestmentCalculatorLimits.YearsMin} and {InvestmentCalculatorLimits.YearsMax}.")
                .WithState(_ => ErrorArgs.NumberRange(InvestmentCalculatorLimits.YearsMin, InvestmentCalculatorLimits.YearsMax));

        RuleFor(x => x.DelayYears)
            .Must((request, delay) => delay >= 0 && delay < request.Years)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.DelayRange)
                .WithMessage("The plan must start before its last year.");

        RuleFor(x => x.StopAfterYear)
            .Must((request, stop) => stop is null || (stop >= 0 && stop <= request.Years))
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.StopAfterYearRange)
                .WithMessage("The plan can stop adding at the latest in its last year.");

        When(x => x.Mode == InvestmentScenarioMode.Goal, () =>
        {
            RuleFor(x => x.GoalTarget)
                .NotNull()
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.GoalTargetRequired)
                    .WithMessage("Enter a goal amount.")
                .GreaterThan(0m)
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.GoalTargetRange)
                    .WithMessage(x => Describe(GoalRangeMessage, InvestmentCalculatorLimits.GoalMax, x))
                    .WithState(x => MoneyArgs(InvestmentCalculatorLimits.GoalMax, x))
                .LessThanOrEqualTo(x => MoneyBounds.Max(InvestmentCalculatorLimits.GoalMax, x.CurrencyCode))
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.GoalTargetRange)
                    .WithMessage(x => Describe(GoalRangeMessage, InvestmentCalculatorLimits.GoalMax, x))
                    .WithState(x => MoneyArgs(InvestmentCalculatorLimits.GoalMax, x))
                .PrecisionScale(18, 2, true)
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.WholeCents)
                    .WithMessage(WholeCentsMessage);

            RuleFor(x => x.GoalSolveFor)
                .NotNull()
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.GoalSolveForRequired)
                    .WithMessage(GoalSolveForMessage)
                .IsInEnum()
                    .WithErrorCode(ApiErrorKeys.InvestmentScenarios.GoalSolveForRequired)
                    .WithMessage(GoalSolveForMessage);
        });
    }

    private const string GoalRangeMessage = "The goal amount must be over {min} and at most {max}.";
    private const string GoalSolveForMessage = "Choose what the goal solves for: how much a month, or how long.";
    private const string WholeCentsMessage = "Amounts cannot have more than 2 decimal places.";

    /// <summary>An amount from 0 to <paramref name="dollarMax"/> in the scenario's currency, in whole cents.</summary>
    private void Amount(Expression<Func<SaveInvestmentScenarioRequestDto, decimal>> field,
        long dollarMax, string key, string label)
    {
        string template = label + " must be between {min} and {max}.";
        RuleFor(field)
            .GreaterThanOrEqualTo(0m)
                .WithErrorCode(key)
                .WithMessage(x => Describe(template, dollarMax, x))
                .WithState(x => MoneyArgs(dollarMax, x))
            .LessThanOrEqualTo(x => MoneyBounds.Max(dollarMax, x.CurrencyCode))
                .WithErrorCode(key)
                .WithMessage(x => Describe(template, dollarMax, x))
                .WithState(x => MoneyArgs(dollarMax, x))
            .PrecisionScale(18, 2, true)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.WholeCents)
                .WithMessage(WholeCentsMessage);
    }

    /// <summary>A rate from 0 to <paramref name="max"/> percent, with at most two decimals (decimal(5,2)).</summary>
    private void Rate(Expression<Func<SaveInvestmentScenarioRequestDto, decimal>> field,
        decimal max, string key, string label)
    {
        RuleFor(field)
            .InclusiveBetween(0m, max)
                .WithErrorCode(key)
                .WithMessage($"{label} must be between 0% and {max.ToString("0.##", CultureInfo.InvariantCulture)}%.")
                .WithState(_ => ErrorArgs.NumberRange(0m, max))
            .PrecisionScale(5, 2, true)
                .WithErrorCode(ApiErrorKeys.InvestmentScenarios.RateDecimals)
                .WithMessage("Rates cannot have more than 2 decimal places.");
    }

    private static string Describe(string template, long dollarMax, SaveInvestmentScenarioRequestDto request) =>
        MoneyBounds.Describe(template, dollarMax, request.CurrencyCode);

    private static IReadOnlyDictionary<string, ApiErrorArgDto> MoneyArgs(long dollarMax,
        SaveInvestmentScenarioRequestDto request) =>
        ErrorArgs.MoneyRange(0m, MoneyBounds.Max(dollarMax, request.CurrencyCode),
            SupportedCurrencies.CodeOrDefault(request.CurrencyCode));
}
