using FluentValidation.Results;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.ApiService.Features.MoneyPriorities.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The validator and the path evaluator agree on which answers a path asks for: for every leaf, the validator requires
/// an answer exactly when the evaluator keeps it on the path.
/// </summary>
public class PriorityQuestionsAgreementTests
{
    private readonly SubmitPriorityWalkRequestValidator _validator = new();

    /// <summary>Every answer filled in, leading to L6.</summary>
    private static readonly PriorityAnswers Everything = new()
    {
        MonthlyAmount = 500m,
        DebtApr = 0m,
        DebtBalance = 5_000m,
        EmergencyFundMonths = 6m,
        MonthlyExpenses = 3_000m,
        HasFullEmployerMatch = true,
        EmployerMatchPercent = 4m,
        HorizonYears = 25,
        AgeBand = RetirementAgeBand.Under50,
        AtContributionLimit = false
    };

    private static PriorityAnswers AnswersFor(string leafId) => leafId switch
    {
        "L1" => Everything with { DebtApr = 22m },
        "L2" => Everything with { EmergencyFundMonths = 1m },
        "L3" => Everything with { HasFullEmployerMatch = false },
        "L4" => Everything with { HorizonYears = 2 },
        "L5" => Everything with { HorizonYears = 5 },
        "L6" => Everything,
        "L7" => Everything with { AtContributionLimit = true },
        _ => throw new ArgumentOutOfRangeException(nameof(leafId))
    };

    /// <summary>Each optional answer: its request property, its required message, and how to clear it.</summary>
    private static readonly (string Property, string Message, Func<PriorityAnswers, bool> Kept,
        Action<SubmitPriorityWalkRequest> Clear)[] OptionalAnswers =
    [
        (nameof(SubmitPriorityWalkRequest.DebtBalance), MoneyPrioritiesMessages.DebtBalanceRequired,
            a => a.DebtBalance is not null, r => r.DebtBalance = null),
        (nameof(SubmitPriorityWalkRequest.EmergencyFundMonths), MoneyPrioritiesMessages.EmergencyFundMonthsRequired,
            a => a.EmergencyFundMonths is not null, r => r.EmergencyFundMonths = null),
        (nameof(SubmitPriorityWalkRequest.MonthlyExpenses), MoneyPrioritiesMessages.MonthlyExpensesRequired,
            a => a.MonthlyExpenses is not null, r => r.MonthlyExpenses = null),
        (nameof(SubmitPriorityWalkRequest.HasFullEmployerMatch), MoneyPrioritiesMessages.HasFullEmployerMatchRequired,
            a => a.HasFullEmployerMatch is not null, r => r.HasFullEmployerMatch = null),
        (nameof(SubmitPriorityWalkRequest.EmployerMatchPercent), MoneyPrioritiesMessages.EmployerMatchPercentRequired,
            a => a.EmployerMatchPercent is not null, r => r.EmployerMatchPercent = null),
        (nameof(SubmitPriorityWalkRequest.HorizonYears), MoneyPrioritiesMessages.HorizonYearsRequired,
            a => a.HorizonYears is not null, r => r.HorizonYears = null),
        (nameof(SubmitPriorityWalkRequest.AgeBand), MoneyPrioritiesMessages.AgeBandRequired,
            a => a.AgeBand is not null, r => r.AgeBand = null),
        (nameof(SubmitPriorityWalkRequest.AtContributionLimit), MoneyPrioritiesMessages.AtContributionLimitRequired,
            a => a.AtContributionLimit is not null, r => r.AtContributionLimit = null)
    ];

    private static SubmitPriorityWalkRequest RequestFrom(PriorityAnswers a) => new()
    {
        MonthlyAmount = a.MonthlyAmount,
        DebtApr = a.DebtApr,
        DebtBalance = a.DebtBalance,
        EmergencyFundMonths = a.EmergencyFundMonths,
        MonthlyExpenses = a.MonthlyExpenses,
        HasFullEmployerMatch = a.HasFullEmployerMatch,
        EmployerMatchPercent = a.EmployerMatchPercent,
        HorizonYears = a.HorizonYears,
        AgeBand = a.AgeBand,
        AtContributionLimit = a.AtContributionLimit
    };

    [Theory]
    [InlineData("L1")]
    [InlineData("L2")]
    [InlineData("L3")]
    [InlineData("L4")]
    [InlineData("L5")]
    [InlineData("L6")]
    [InlineData("L7")]
    public void Validator_RequiresExactlyTheAnswersTheEvaluatorKeeps(string leafId)
    {
        PriorityAnswers answers = AnswersFor(leafId);
        PriorityPath path = PriorityPathEvaluator.Evaluate(answers);
        Assert.Equal(leafId, path.LeafId);

        foreach ((string property, string message, Func<PriorityAnswers, bool> kept,
                     Action<SubmitPriorityWalkRequest> clear) in OptionalAnswers)
        {
            SubmitPriorityWalkRequest request = RequestFrom(answers);
            clear(request);
            ValidationResult result = _validator.Validate(request);
            bool required = result.Errors.Any(e => e.PropertyName == property && e.ErrorMessage == message);

            Assert.True(kept(path.Answers) == required,
                $"{leafId}: the evaluator {(kept(path.Answers) ? "keeps" : "clears")} {property}, but the validator "
                + $"{(required ? "requires" : "does not require")} it.");
        }
    }
}
