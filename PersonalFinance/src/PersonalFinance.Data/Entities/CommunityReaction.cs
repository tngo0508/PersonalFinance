using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// A Reaction (GLOSSARY.md): an Account's Helpful or Cheering you on, given to exactly one Strategy or one Comment.
/// One live row per Account, kind and target; taking a Reaction back soft-deletes the row. A Strategy shows how many
/// of each kind it has; a Comment shows only whether the viewer reacted (ADR 0018).
/// </summary>
public class CommunityReaction : AuditableEntity
{
    /// <summary>Which Reaction it is.</summary>
    public CommunityReactionKind Kind { get; set; }

    /// <summary>The Strategy reacted to; null when the target is a Comment.</summary>
    public int? StrategyId { get; set; }

    /// <summary>The Comment reacted to; null when the target is a Strategy.</summary>
    public int? CommentId { get; set; }

    /// <summary>Navigation: the Strategy reacted to, if any.</summary>
    public CommunityStrategy? Strategy { get; set; }

    /// <summary>Navigation: the Comment reacted to, if any.</summary>
    public CommunityComment? Comment { get; set; }
}
