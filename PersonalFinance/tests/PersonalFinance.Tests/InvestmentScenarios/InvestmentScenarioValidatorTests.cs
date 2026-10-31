using FluentValidation.Results;
using PersonalFinance.ApiService.Features.InvestmentScenarios.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.InvestmentScenarios;

/// <summary>
/// The Investment Scenario validators: the calculator's input bounds (#587), with the money bounds scaled to the
/// scenario's currency, the name and note lengths, and the goal a Goal scenario needs. Every failure carries its
/// <see cref="ApiErrorKeys.InvestmentScenarios"/> key (ADR 0017).
/// </summary>
public class InvestmentScenarioValidatorTests
{
    private readonly SaveInvestmentScenarioRequestValidator _validator = new();

    private static SaveInvestmentScenarioRequestDto Valid() => new()
    {
        Name = "Retire at 60",
        Mode = InvestmentScenarioMode.Grow,
        CurrencyCode = "USD",
        InitialAmount = 10_000m,
        MonthlyContribution = 500m,
        AssumedReturn = 7m,
        Years = 20,
        ContributionIncrease = 0m,
        Inflation = 2.5m,
        Fee = 0.1m
    };

    private void AssertValid(SaveInvestmentScenarioRequestDto request)
    {
        ValidationResult result = _validator.Validate(request);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorCode}")));
    }

    private ValidationFailure AssertRejected(SaveInvestmentScenarioRequestDto request, string key)
    {
        ValidationResult result = _validator.Validate(request);
        Assert.False(result.IsValid);
        return Assert.Single(result.Errors, e => e.ErrorCode == key);
    }

    [Fact]
    public void ADefaultPlan_IsValid() => AssertValid(Valid());

    [Fact]
    public void EveryBound_ItsEdgeIsValid()
    {
        SaveInvestmentScenarioRequestDto low = Valid();
        (low.InitialAmount, low.MonthlyContribution, low.AssumedReturn, low.Years) = (0m, 0m, 0m, 1);
        (low.ContributionIncrease, low.Inflation, low.Fee, low.DelayYears, low.StopAfterYear) = (0m, 0m, 0m, 0, 0);
        AssertValid(low);

        SaveInvestmentScenarioRequestDto high = Valid();
        (high.InitialAmount, high.MonthlyContribution) = (100_000_000m, 1_000_000m);
        (high.AssumedReturn, high.Years, high.ContributionIncrease, high.Inflation, high.Fee) = (15m, 50, 10m, 10m, 3m);
        (high.DelayYears, high.StopAfterYear) = (49, 50);
        AssertValid(high);
    }

    public static TheoryData<string, Action<SaveInvestmentScenarioRequestDto>> OutOfBounds => new()
    {
        { ApiErrorKeys.InvestmentScenarios.InitialAmountRange, r => r.InitialAmount = -1m },
        { ApiErrorKeys.InvestmentScenarios.InitialAmountRange, r => r.InitialAmount = 100_000_000.01m },
        { ApiErrorKeys.InvestmentScenarios.MonthlyContributionRange, r => r.MonthlyContribution = -0.01m },
        { ApiErrorKeys.InvestmentScenarios.MonthlyContributionRange, r => r.MonthlyContribution = 1_000_001m },
        { ApiErrorKeys.InvestmentScenarios.AssumedReturnRange, r => r.AssumedReturn = -0.5m },
        { ApiErrorKeys.InvestmentScenarios.AssumedReturnRange, r => r.AssumedReturn = 15.01m },
        { ApiErrorKeys.InvestmentScenarios.YearsRange, r => r.Years = 0 },
        { ApiErrorKeys.InvestmentScenarios.YearsRange, r => r.Years = 51 },
        { ApiErrorKeys.InvestmentScenarios.ContributionIncreaseRange, r => r.ContributionIncrease = 10.5m },
        { ApiErrorKeys.InvestmentScenarios.InflationRange, r => r.Inflation = -1m },
        { ApiErrorKeys.InvestmentScenarios.InflationRange, r => r.Inflation = 10.1m },
        { ApiErrorKeys.InvestmentScenarios.FeeRange, r => r.Fee = 3.01m },
        { ApiErrorKeys.InvestmentScenarios.DelayRange, r => r.DelayYears = -1 },
        { ApiErrorKeys.InvestmentScenarios.DelayRange, r => r.DelayYears = 20 },
        { ApiErrorKeys.InvestmentScenarios.StopAfterYearRange, r => r.StopAfterYear = -1 },
        { ApiErrorKeys.InvestmentScenarios.StopAfterYearRange, r => r.StopAfterYear = 21 },
        { ApiErrorKeys.InvestmentScenarios.WholeCents, r => r.MonthlyContribution = 500.005m },
        { ApiErrorKeys.InvestmentScenarios.RateDecimals, r => r.AssumedReturn = 7.125m },
        { ApiErrorKeys.InvestmentScenarios.NameRequired, r => r.Name = "   " },
        { ApiErrorKeys.InvestmentScenarios.NameTooLong, r => r.Name = new string('a', 101) },
        { ApiErrorKeys.InvestmentScenarios.NoteTooLong, r => r.Note = new string('n', 501) },
        { ApiErrorKeys.InvestmentScenarios.ModeInvalid, r => r.Mode = (InvestmentScenarioMode)7 },
        { ApiErrorKeys.InvestmentScenarios.CurrencyUnsupported, r => r.CurrencyCode = "XYZ" }
    };

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public void AnInputOutOfBounds_IsRejectedWithItsKey(string key, Action<SaveInvestmentScenarioRequestDto> change)
    {
        SaveInvestmentScenarioRequestDto request = Valid();
        change(request);

        AssertRejected(request, key);
    }

    [Fact]
    public void TheMoneyBounds_AreScaledToTheScenariosCurrency_AndCarriedAsMoneyArguments()
    {
        SaveInvestmentScenarioRequestDto dong = Valid();
        (dong.CurrencyCode, dong.InitialAmount) = ("VND", 2_500_000_000_000m);
        AssertValid(dong);

        dong.InitialAmount = 2_500_000_000_001m;
        ValidationFailure failure = AssertRejected(dong, ApiErrorKeys.InvestmentScenarios.InitialAmountRange);

        var args = (IReadOnlyDictionary<string, ApiErrorArgDto>)failure.CustomState;
        Assert.Equal((ApiErrorArgKind.Money, "2500000000000", "VND"), (args["max"].Kind, args["max"].Value, args["max"].Currency));
        Assert.Equal("0", args["min"].Value);
    }

    [Fact]
    public void AGoalScenario_NeedsAGoalAmountAndAQuestion()
    {
        SaveInvestmentScenarioRequestDto request = Valid();
        request.Mode = InvestmentScenarioMode.Goal;

        AssertRejected(request, ApiErrorKeys.InvestmentScenarios.GoalTargetRequired);
        AssertRejected(request, ApiErrorKeys.InvestmentScenarios.GoalSolveForRequired);

        (request.GoalTarget, request.GoalSolveFor) = (500_000m, InvestmentGoalSolveFor.Monthly);
        AssertValid(request);

        request.GoalTarget = 1_000_000_001m;
        AssertRejected(request, ApiErrorKeys.InvestmentScenarios.GoalTargetRange);

        (request.GoalTarget, request.GoalSolveFor) = (0m, (InvestmentGoalSolveFor)9);
        AssertRejected(request, ApiErrorKeys.InvestmentScenarios.GoalTargetRange);
        AssertRejected(request, ApiErrorKeys.InvestmentScenarios.GoalSolveForRequired);
    }

    [Fact]
    public void AGrowScenario_IgnoresAnyGoal()
    {
        SaveInvestmentScenarioRequestDto request = Valid();
        (request.GoalTarget, request.GoalSolveFor) = (-5m, (InvestmentGoalSolveFor)9);

        AssertValid(request);
    }

    [Fact]
    public void ADuplicateName_FollowsTheSameNameRules()
    {
        var validator = new DuplicateInvestmentScenarioRequestValidator();

        Assert.True(validator.Validate(new DuplicateInvestmentScenarioRequestDto { Name = "Copy of plan" }).IsValid);
        Assert.Equal(ApiErrorKeys.InvestmentScenarios.NameRequired,
            Assert.Single(validator.Validate(new DuplicateInvestmentScenarioRequestDto { Name = "" }).Errors).ErrorCode);
        Assert.Equal(ApiErrorKeys.InvestmentScenarios.NameTooLong,
            Assert.Single(validator.Validate(new DuplicateInvestmentScenarioRequestDto { Name = new string('x', 101) }).Errors).ErrorCode);
    }
}
