namespace PersonalFinance.Data.Entities;

/// <summary>
/// A Check-in (GLOSSARY.md): an Account's private record that it kept a Challenge's habit for one period. One live row
/// per Account, Challenge and period. Only its own Account ever reads it back; others see only how many checked in.
/// </summary>
public class CommunityCheckIn : AuditableEntity
{
    public int ChallengeId { get; set; }

    /// <summary>The first local day of the period it counts for: the day itself, or the first day of the week.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Navigation: the Challenge checked in to.</summary>
    public CommunityChallenge? Challenge { get; set; }
}
