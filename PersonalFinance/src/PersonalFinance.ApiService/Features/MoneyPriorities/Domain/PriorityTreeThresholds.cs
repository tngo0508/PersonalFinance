namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The thresholds Priority Tree version 1 decides on (#300). The page states each one as an assumption.
/// </summary>
internal static class PriorityTreeThresholds
{
    /// <summary>Q1: a debt APR at or above this percentage is high-interest.</summary>
    public const decimal HighInterestAprPercent = 7m;

    /// <summary>Q2: an emergency fund covering at least this many months of expenses is covered (L2's target).</summary>
    public const decimal EmergencyFundTargetMonths = 3m;

    /// <summary>Q4: a horizon under this many years is short (L4).</summary>
    public const int ShortHorizonYears = 3;

    /// <summary>Q4: a horizon over this many years is long (Q5); from <see cref="ShortHorizonYears"/> up to this is L5.</summary>
    public const int LongHorizonYears = 10;
}
