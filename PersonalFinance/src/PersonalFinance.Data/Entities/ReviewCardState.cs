namespace PersonalFinance.Data.Entities;

/// <summary>
/// What an Account last said about one Review flash card (GLOSSARY.md): the Term Entry of a finished Lesson. One row
/// per Account and Term among non-deleted rows, written the first time the card is marked. A card retires after two
/// "Knew it" in a row.
/// </summary>
public class ReviewCardState : AuditableEntity
{
    /// <summary>The Term Entry's permanent slug (<c>apr</c>), a file in the content folder (ADR 0019).</summary>
    public string TermSlug { get; set; } = string.Empty;

    /// <summary><c>true</c> after "Knew it", <c>false</c> after "Not yet".</summary>
    public bool KnewIt { get; set; }

    /// <summary>How many "Knew it" in a row; "Not yet" resets it to 0.</summary>
    public int KnewItStreak { get; set; }

    /// <summary>When the card was last marked (UTC); the least recently marked cards are drawn first.</summary>
    public DateTime LastShownAt { get; set; }
}
