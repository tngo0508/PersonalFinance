using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// A Challenge (GLOSSARY.md): a time-boxed habit an Admin defines and Accounts join. Shared data with no owner, never
/// deleted, untouched by the Purge. It has no amount field: a Challenge is about behavior, never money (ADR 0018).
/// </summary>
public class CommunityChallenge
{
    public int Id { get; set; }

    /// <summary>Its short name ("Cook at home").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The habit to keep, in a sentence or two.</summary>
    public string Habit { get; set; } = string.Empty;

    /// <summary>How often it asks for a Check-in.</summary>
    public CommunityChallengePeriod Period { get; set; }

    /// <summary>The first local day it runs.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>The last local day it runs.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>When the Admin wrote it (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }
}
