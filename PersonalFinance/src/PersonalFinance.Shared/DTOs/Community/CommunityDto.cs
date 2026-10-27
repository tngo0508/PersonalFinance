using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Community;

/// <summary>
/// DTO for returning comment data to the view/API. Authors are Pseudonyms only: no Community DTO carries a display
/// name or email (ADR 0018).
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
    public int Likes { get; set; }
    public bool IsLikedByUser { get; set; }
    public int? PollId { get; set; }
    public int? StrategyId { get; set; }
}

/// <summary>
/// Result of a comment like toggle operation.
/// </summary>
public sealed class CommentLikeResult
{
    public bool Success { get; set; }
    public int NewLikeCount { get; set; }
    public bool IsLiked { get; set; }
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
}

/// <summary>
/// The viewing Account's Pseudonym (GLOSSARY.md).
/// </summary>
public sealed class CommunityPseudonymDto
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Full Guild Hall response DTO: the viewer's Pseudonym, the polls, the board's Strategies and the comments.
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

    public List<CommunityCommentDto> RecentComments { get; set; } = new();
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
/// Request to add a comment.
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
