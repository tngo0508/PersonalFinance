namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Where a Savings Goal stands against its target date (GLOSSARY.md: Pace). The first that applies wins, in this order.
/// </summary>
public enum PaceStatus
{
    /// <summary>The saved amount meets or passes the target.</summary>
    Reached = 0,

    /// <summary>The goal has no target date to compare with.</summary>
    NoTargetDate = 1,

    /// <summary>The target date is before today.</summary>
    PastDue = 2,

    /// <summary>The goal is under 30 days old: too early for an on-track verdict.</summary>
    JustStarted = 3,

    /// <summary>At least 5% of the target above the ideal line to the target date.</summary>
    Ahead = 4,

    /// <summary>Within 5% of the target of the ideal line.</summary>
    OnTrack = 5,

    /// <summary>More than 5% of the target below the ideal line.</summary>
    Behind = 6
}
