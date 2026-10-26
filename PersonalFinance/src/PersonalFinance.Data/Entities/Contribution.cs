using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// One dated movement of money into or out of a Savings Goal (GLOSSARY.md). A goal's saved amount is the total of its
/// Contributions. It carries no currency of its own: it is in its goal's currency.
/// </summary>
public class Contribution : AuditableEntity
{
    /// <summary>Foreign key to the goal the money moved into or out of.</summary>
    public int SavingsGoalId { get; set; }

    /// <summary>Signed, never zero: positive is a deposit, negative a withdrawal.</summary>
    public decimal Amount { get; set; }

    /// <summary>Calendar date the money moved.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Optional short note, e.g. "Tax refund".</summary>
    public string? Note { get; set; }

    /// <summary>Whether the user recorded it, it is the goal's starting amount, or a balance correction made it.</summary>
    public ContributionKind Kind { get; set; }

    /// <summary>Navigation property to the goal.</summary>
    public SavingsGoal? SavingsGoal { get; set; }
}
