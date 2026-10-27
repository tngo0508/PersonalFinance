using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// A Strategy (GLOSSARY.md): a practice an Account tried, told as what they tried, what happened and what they keep
/// doing, with a category. It starts <see cref="CommunityStrategyStatus.Pending"/>; other users see it only once an
/// Admin approves it (ADR 0018). The author is the Account in <see cref="AuditableEntity.UserId"/>, shown by its
/// <see cref="CommunityPseudonym"/>; no name is stored here.
/// </summary>
public class CommunityStrategy : AuditableEntity
{
    /// <summary>One of <c>CommunityStrategyLimits.Categories</c>, stored in English.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The practice's name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What the author tried.</summary>
    public string Tried { get; set; } = string.Empty;

    /// <summary>What happened when they did.</summary>
    public string Happened { get; set; } = string.Empty;

    /// <summary>What they keep doing.</summary>
    public string Keeping { get; set; } = string.Empty;

    /// <summary>Where the Strategy is in the Admin's review.</summary>
    public CommunityStrategyStatus Status { get; set; } = CommunityStrategyStatus.Pending;

    /// <summary>When an Admin last approved or rejected it; null while it has never been reviewed.</summary>
    public DateTime? ReviewedAt { get; set; }
}
