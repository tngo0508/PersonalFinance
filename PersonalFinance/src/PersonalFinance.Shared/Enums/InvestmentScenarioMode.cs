namespace PersonalFinance.Shared.Enums;

/// <summary>
/// What an Investment Scenario (GLOSSARY.md) was saved for: growing a plan, or reaching a goal with it. A Goal
/// scenario also carries the goal amount and what the goal tile solves for.
/// </summary>
public enum InvestmentScenarioMode
{
    /// <summary>A plan saved to see how it grows.</summary>
    Grow = 1,

    /// <summary>A plan saved with a goal amount the goal tile solves for.</summary>
    Goal = 2
}
