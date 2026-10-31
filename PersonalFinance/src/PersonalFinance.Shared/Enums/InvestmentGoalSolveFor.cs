namespace PersonalFinance.Shared.Enums;

/// <summary>
/// The question the Investment Calculator's goal tile answers for a goal amount.
/// </summary>
public enum InvestmentGoalSolveFor
{
    /// <summary>"How much a month?": the monthly contribution that reaches the goal in the plan's years.</summary>
    Monthly = 1,

    /// <summary>"How long?": the years the plan's monthly contribution takes to reach the goal.</summary>
    Years = 2
}
