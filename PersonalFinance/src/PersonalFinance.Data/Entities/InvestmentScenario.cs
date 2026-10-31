using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Investment Scenario (GLOSSARY.md): a saved, named set of Investment Calculator inputs, never results (#592).
/// The page works the projection out from these inputs each time, so the server never computes one.
/// </summary>
public class InvestmentScenario : AuditableEntity, IInvestmentScenarioInputs
{
    /// <summary>The scenario's name (at most 100 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An optional note (at most 500 characters).</summary>
    public string? Note { get; set; }

    /// <summary>Saved to grow a plan, or to reach a goal with it.</summary>
    public InvestmentScenarioMode Mode { get; set; } = InvestmentScenarioMode.Grow;

    /// <summary>
    /// ISO 4217 code, one of <see cref="SupportedCurrencies"/> (ADR 0016): every amount of the scenario is in it, so a
    /// later change of Reporting Currency never reinterprets them.
    /// </summary>
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    public decimal InitialAmount { get; set; }

    public decimal MonthlyContribution { get; set; }

    /// <summary>The assumed yearly return, in percent.</summary>
    public decimal AssumedReturn { get; set; }

    public int Years { get; set; }

    /// <summary>The yearly raise of the monthly contribution, in percent.</summary>
    public decimal ContributionIncrease { get; set; }

    /// <summary>The yearly inflation, in percent.</summary>
    public decimal Inflation { get; set; }

    /// <summary>The yearly fund fee, in percent.</summary>
    public decimal Fee { get; set; }

    /// <summary>Years before the plan starts (an applied "start later" what-if, #591); 0 starts now.</summary>
    public int DelayYears { get; set; }

    /// <summary>The year after which the plan stops adding (an applied "stop adding" what-if); null keeps adding.</summary>
    public int? StopAfterYear { get; set; }

    /// <summary>The goal amount; null when the scenario has no goal.</summary>
    public decimal? GoalTarget { get; set; }

    /// <summary>What the goal tile solves for; null when the scenario has no goal.</summary>
    public InvestmentGoalSolveFor? GoalSolveFor { get; set; }
}
