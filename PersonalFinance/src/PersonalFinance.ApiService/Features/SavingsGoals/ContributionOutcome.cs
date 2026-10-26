namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// What became of a Contribution change.
/// </summary>
public enum ContributionOutcome
{
    /// <summary>Saved, with the goal's saved amount rewritten in the same save.</summary>
    Saved,

    /// <summary>The goal or Contribution does not exist for this user.</summary>
    NotFound,

    /// <summary>Refused: the change would leave the saved amount below zero.</summary>
    BelowZero,

    /// <summary>Refused: an Adjustment can only be deleted, not edited.</summary>
    NotEditable
}
