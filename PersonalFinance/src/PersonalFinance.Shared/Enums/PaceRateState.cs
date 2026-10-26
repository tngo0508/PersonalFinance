namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Whether a Savings Goal's recent rate (the net of its Regular Contributions over the last 90 days) can project a
/// finish date.
/// </summary>
public enum PaceRateState
{
    /// <summary>Not enough history: the goal is under 30 days old or has no Regular Contribution.</summary>
    Unknown = 0,

    /// <summary>No Regular Contribution in the window, or their net is zero or less.</summary>
    Stalled = 1,

    /// <summary>A positive rate per month.</summary>
    Positive = 2
}
