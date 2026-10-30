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
/// An option of a Poll. Results are a rounded share only, never a count (ADR 0018).
/// </summary>
public sealed class CommunityPollOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Its share of the votes as a whole percentage, the shares adding up to 100 once anyone voted; null until the
    /// viewing Account has voted, so results never sway a vote.
    /// </summary>
    public int? Percentage { get; set; }
}

/// <summary>
/// An open Poll as the viewing Account sees it. It carries no vote totals, so results inform without becoming a
/// scoreboard.
/// </summary>
public sealed class CommunityPollDto
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<CommunityPollOptionDto> Options { get; set; } = new();
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
/// Full Guild Hall response DTO: the viewer's Pseudonym, the polls, the board's Strategies, each with its Comments,
/// and the Challenges.
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

    /// <summary>The Challenges running or about to start, running ones first.</summary>
    public List<CommunityChallengeDto> Challenges { get; set; } = new();
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

/// <summary>
/// An Admin's new Poll. It is stored closed, for the Admin to open when it is ready.
/// </summary>
public sealed class CommunityPollRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>One of the board's categories (<c>CommunityStrategyLimits.Categories</c>).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The options' texts, in the order they are shown.</summary>
    public List<string> Options { get; set; } = new();
}

/// <summary>
/// A Poll in the Admin list, open or closed. Unlike what users see, it carries the number of votes.
/// </summary>
public sealed class CommunityPollAdminItemDto
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The options' texts, in the order they are shown.</summary>
    public List<string> Options { get; set; } = new();

    public int VoteCount { get; set; }
}

/// <summary>
/// An Admin's decision to open a Poll for voting or close it.
/// </summary>
public sealed class CommunityPollStatusRequestDto
{
    public bool IsOpen { get; set; }
}

/// <summary>
/// A Challenge as the viewing Account sees it: running or about to start. Its Check-ins are the viewer's own and stay
/// private (ADR 0018); of everyone else it shows only how many checked in this period, never who.
/// </summary>
public sealed class CommunityChallengeDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Habit { get; set; } = string.Empty;
    public CommunityChallengePeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// The first day of the period the viewer is in, in its own time zone; null until the Challenge starts.
    /// </summary>
    public DateOnly? CurrentPeriodStart { get; set; }

    /// <summary>Whether the viewer has joined it.</summary>
    public bool IsMember { get; set; }

    /// <summary>Whether the viewer has checked in for the current period.</summary>
    public bool CheckedInThisPeriod { get; set; }

    /// <summary>How many periods in a row the viewer kept, up to the current one; only the viewer sees it.</summary>
    public int Streak { get; set; }

    /// <summary>How many Accounts checked in for the current period, the viewer included.</summary>
    public int CheckedInCount { get; set; }

    /// <summary>How many Accounts joined it, the viewer included.</summary>
    public int MemberCount { get; set; }
}

/// <summary>
/// Request to join a Challenge or check in to it, with the browser's time zone, which decides the viewer's local day.
/// </summary>
public sealed class CommunityChallengeActionRequestDto
{
    /// <summary>The IANA time zone (<c>Intl.DateTimeFormat().resolvedOptions().timeZone</c>); UTC when missing or unknown.</summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// An Admin's new Challenge: a habit, never an amount of money (ADR 0018), over a span of local days.
/// </summary>
public sealed class CommunityChallengeRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Habit { get; set; } = string.Empty;
    public CommunityChallengePeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

/// <summary>
/// A Challenge in the Admin list, with how many Accounts joined it.
/// </summary>
public sealed class CommunityChallengeAdminItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Habit { get; set; } = string.Empty;
    public CommunityChallengePeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int MemberCount { get; set; }
}
