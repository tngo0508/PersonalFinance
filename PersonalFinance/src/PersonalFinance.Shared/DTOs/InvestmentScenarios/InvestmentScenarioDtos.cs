using System.Text.Json.Serialization;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.InvestmentScenarios;

/// <summary>
/// An Investment Scenario (GLOSSARY.md): a saved, named set of Investment Calculator inputs, never results. Every
/// amount is in <see cref="CurrencyCode"/>, the currency it was saved in (ADR 0016); the page works the projection out
/// from the inputs each time.
/// </summary>
public class InvestmentScenarioDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Note { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InvestmentScenarioMode Mode { get; set; } = InvestmentScenarioMode.Grow;

    /// <summary>The ISO 4217 code every amount of the scenario is in.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

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

    /// <summary>Years before the plan starts (an applied "start later" what-if); 0 starts now.</summary>
    public int DelayYears { get; set; }

    /// <summary>The year after which the plan stops adding (an applied "stop adding" what-if); null keeps adding.</summary>
    public int? StopAfterYear { get; set; }

    /// <summary>The goal amount; null when the scenario has no goal.</summary>
    public decimal? GoalTarget { get; set; }

    /// <summary>What the goal tile solves for; null when the scenario has no goal.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InvestmentGoalSolveFor? GoalSolveFor { get; set; }

    /// <summary>When the scenario was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When the scenario was last saved: its last update, else its creation (UTC). The list's order.</summary>
    public DateTime LastEditedAtUtc { get; set; }
}

/// <summary>
/// Saves an Investment Scenario: creates one, or replaces every field of an existing one (a rename sends the stored
/// inputs back with the new name). The currency, when given, must be supported; when missing, a new scenario is in
/// US dollars and an existing one keeps its currency.
/// </summary>
public class SaveInvestmentScenarioRequestDto
{
    public string Name { get; set; } = string.Empty;

    public string? Note { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InvestmentScenarioMode Mode { get; set; } = InvestmentScenarioMode.Grow;

    public string? CurrencyCode { get; set; }

    public decimal InitialAmount { get; set; }

    public decimal MonthlyContribution { get; set; }

    public decimal AssumedReturn { get; set; }

    public int Years { get; set; }

    public decimal ContributionIncrease { get; set; }

    public decimal Inflation { get; set; }

    public decimal Fee { get; set; }

    public int DelayYears { get; set; }

    public int? StopAfterYear { get; set; }

    public decimal? GoalTarget { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InvestmentGoalSolveFor? GoalSolveFor { get; set; }
}

/// <summary>
/// Duplicates an Investment Scenario under a new name (the page writes "Copy of …" in the reader's language).
/// </summary>
public class DuplicateInvestmentScenarioRequestDto
{
    public string Name { get; set; } = string.Empty;
}
