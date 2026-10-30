using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Community;

/// <summary>
/// A Comment on a Strategy or a Poll, as the viewing Account sees it. Authors are Pseudonyms only: no Community DTO
/// carries a display name or email (ADR 0018). A Comment carries no Reaction counts, only whether the viewer reacted,
/// so replies never become a popularity contest.
/// </summary>
public sealed class CommunityCommentDto
{
    public int Id { get; set; }

    /// <summary>The author's Pseudonym, or null once the author is a Former member (purged, ADR 0005).</summary>
    public string? AuthorPseudonym { get; set; }

    /// <summary>Whether the viewing Account wrote it, so the page can show a re-rolled Pseudonym on it at once.</summary>
    public bool IsOwn { get; set; }

    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Whether the viewer reacted Helpful.</summary>
    public bool HelpfulByViewer { get; set; }

    /// <summary>Whether the viewer reacted Cheering you on.</summary>
    public bool CheeringByViewer { get; set; }

    public int? PollId { get; set; }
    public int? StrategyId { get; set; }
}

/// <summary>
/// Request to give or take back a Reaction: its kind, and exactly one of a Strategy or a Comment.
/// </summary>
public sealed class CommunityReactionRequestDto
{
    public CommunityReactionKind Kind { get; set; }
    public int? StrategyId { get; set; }
    public int? CommentId { get; set; }
}

/// <summary>
/// A Reaction toggle's outcome: whether the viewer now holds it, and, on a Strategy only, the kind's new count.
/// </summary>
public sealed class CommunityReactionResultDto
{
    public CommunityReactionKind Kind { get; set; }

    /// <summary>Whether the viewer holds the Reaction after the toggle.</summary>
    public bool ByViewer { get; set; }

    /// <summary>How many Accounts gave the kind to the Strategy; always null for a Comment, which shows no counts.</summary>
    public int? Count { get; set; }
}

/// <summary>
/// A Comment in the Admin moderation list, newest first, with what it replies to. Authors are Pseudonyms only.
/// </summary>
public sealed class CommunityCommentReviewItemDto
{
    public int Id { get; set; }

    /// <summary>The author's Pseudonym, or null once the author is a Former member.</summary>
    public string? AuthorPseudonym { get; set; }

    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Whether an Admin has hidden it.</summary>
    public bool IsHidden { get; set; }

    /// <summary>The title of the Strategy it replies to, if it is on a Strategy.</summary>
    public string? StrategyTitle { get; set; }

    /// <summary>The question of the Poll it replies to, if it is on a Poll.</summary>
    public string? PollQuestion { get; set; }

    /// <summary>Whether its text holds a money amount by <c>MoneyAmountRule</c>.</summary>
    public bool HasAmount { get; set; }
}

/// <summary>
/// An Admin's decision on a Comment: hide it, or show it again.
/// </summary>
public sealed class CommunityCommentVisibilityRequestDto
{
    public bool IsHidden { get; set; }
}

/// <summary>
/// A Strategy on the board, as the viewing Account sees it: an Approved one, or one of the viewer's own in any status.
/// Authors are Pseudonyms only (ADR 0018).
/// </summary>
public sealed class CommunityStrategyDto
{
    public int Id { get; set; }

    /// <summary>The author's Pseudonym, or null once the author is a Former member (purged, ADR 0005).</summary>
    public string? AuthorPseudonym { get; set; }

    /// <summary>Whether the viewing Account wrote it, so the page can show a re-rolled Pseudonym on it at once.</summary>
    public bool IsOwn { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tried { get; set; } = string.Empty;
    public string Happened { get; set; } = string.Empty;
    public string Keeping { get; set; } = string.Empty;

    /// <summary>Pending or Rejected only on the viewer's own Strategies; everyone else's are Approved.</summary>
    public CommunityStrategyStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>How many Accounts found it Helpful.</summary>
    public int HelpfulCount { get; set; }

    /// <summary>How many Accounts are Cheering its author on.</summary>
    public int CheeringCount { get; set; }

    /// <summary>Whether the viewer reacted Helpful.</summary>
    public bool HelpfulByViewer { get; set; }

    /// <summary>Whether the viewer reacted Cheering you on.</summary>
    public bool CheeringByViewer { get; set; }

    /// <summary>Its shown Comments, oldest first; always empty while it is not Approved.</summary>
    public List<CommunityCommentDto> Comments { get; set; } = new();
}

/// <summary>
/// A Strategy in the Admin review queue. <see cref="HasAmount"/> flags one that names a money amount (ADR 0018); the
/// flag never blocks it. Authors are Pseudonyms only, here too.
/// </summary>
public sealed class CommunityStrategyReviewItemDto
{
    public int Id { get; set; }

    /// <summary>The author's Pseudonym, or null once the author is a Former member.</summary>
    public string? AuthorPseudonym { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tried { get; set; } = string.Empty;
    public string Happened { get; set; } = string.Empty;
    public string Keeping { get; set; } = string.Empty;
    public CommunityStrategyStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>Whether the title or any of the texts holds a money amount by <c>MoneyAmountRule</c>.</summary>
    public bool HasAmount { get; set; }
}

/// <summary>
/// DTO for a poll option.
/// </summary>
public sealed class CommunityPollOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Votes { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// DTO for a community poll.
/// </summary>
public sealed class CommunityPollDto
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<CommunityPollOptionDto> Options { get; set; } = new();
    public int TotalVotes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasVoted { get; set; }
    public int? VotedOptionId { get; set; }

    /// <summary>Its shown Comments, oldest first.</summary>
    public List<CommunityCommentDto> Comments { get; set; } = new();
}

/// <summary>
/// The viewing Account's Pseudonym (GLOSSARY.md).
/// </summary>
public sealed class CommunityPseudonymDto
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Full Guild Hall response DTO: the viewer's Pseudonym, the polls and the board's Strategies, each with its Comments.
/// </summary>
public sealed class CommunityResponseDto
{
    /// <summary>The viewing Account's Pseudonym, created on its first visit.</summary>
    public string Pseudonym { get; set; } = string.Empty;

    public List<CommunityPollDto> ActivePolls { get; set; } = new();

    /// <summary>
    /// The board: the viewer's own Pending and Rejected Strategies first, then Approved ones, newest first, in the
    /// requested category if one was given.
    /// </summary>
    public List<CommunityStrategyDto> Strategies { get; set; } = new();
}

/// <summary>
/// Request to vote on a poll.
/// </summary>
public sealed class CommunityVoteRequestDto
{
    public string PollCode { get; set; } = string.Empty;
    public int OptionId { get; set; }
}

/// <summary>
/// Request to add a Comment to a Poll (by its code) or an Approved Strategy (by its id).
/// </summary>
public sealed class CommunityCommentRequestDto
{
    public string Text { get; set; } = string.Empty;
    public string? PollCode { get; set; }
    public int? StrategyId { get; set; }
}

/// <summary>
/// Request to pin a Strategy. It is stored Pending, for an Admin to review.
/// </summary>
public sealed class CommunityStrategyRequestDto
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tried { get; set; } = string.Empty;
    public string Happened { get; set; } = string.Empty;
    public string Keeping { get; set; } = string.Empty;
}

/// <summary>
/// An Admin's decision on a Strategy: Approved or Rejected.
/// </summary>
public sealed class CommunityStrategyReviewRequestDto
{
    public CommunityStrategyStatus Status { get; set; }
}
