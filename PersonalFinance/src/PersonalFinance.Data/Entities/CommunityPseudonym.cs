namespace PersonalFinance.Data.Entities;

/// <summary>
/// An Account's Pseudonym (GLOSSARY.md): the generated "Adjective Noun" name it appears under everywhere in the Guild
/// Hall (ADR 0018). One row per Account, created the first time the Account opens the Guild Hall or posts, and
/// replaced in place by a re-roll: stories and comments reference the Account, not the name, so every past post shows
/// the current one. The Purge hard-deletes the row like every other user-owned row.
/// </summary>
public class CommunityPseudonym : AuditableEntity
{
    /// <summary>The name, unique across Accounts; soft-deleted rows keep theirs reserved.</summary>
    public string Name { get; set; } = string.Empty;
}
