using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.InvestmentScenarios;

/// <summary>
/// The twelve Investment Calculator inputs an Investment Scenario keeps (GLOSSARY.md), shared by the entity, its DTO
/// and the save request so the inputs are copied in one place (InvestmentScenarioService).
/// </summary>
public interface IInvestmentScenarioInputs
{
    InvestmentScenarioMode Mode { get; }

    decimal InitialAmount { get; }

    decimal MonthlyContribution { get; }

    decimal AssumedReturn { get; }

    int Years { get; }

    decimal ContributionIncrease { get; }

    decimal Inflation { get; }

    decimal Fee { get; }

    int DelayYears { get; }

    int? StopAfterYear { get; }

    decimal? GoalTarget { get; }

    InvestmentGoalSolveFor? GoalSolveFor { get; }
}
