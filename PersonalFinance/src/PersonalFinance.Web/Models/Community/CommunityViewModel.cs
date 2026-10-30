using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// View model for the Guild Hall page: the viewer's Pseudonym, polls, the board of Strategies, each with its
/// Comments, and the Challenges.
/// </summary>
public sealed class CommunityViewModel
{
    /// <summary>
    /// Gets or sets the viewer's Pseudonym, the name it appears under everywhere on the page.
    /// </summary>
    public string Pseudonym { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the open Polls, newest first.
    /// </summary>
    public List<CommunityPoll> ActivePolls { get; set; } = new();

    /// <summary>
    /// Gets or sets the Strategies on the board: the viewer's own Pending and Rejected ones first, then Approved ones.
    /// </summary>
    public List<CommunityStrategy> Strategies { get; set; } = new();

    /// <summary>
    /// Gets or sets the category the board is filtered to, or null for every category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the Challenges running or about to start, running ones first.
    /// </summary>
    public List<CommunityChallenge> Challenges { get; set; } = new();
}

/// <summary>
/// An open Poll. It carries no vote totals: results are shares only, shown once the viewer has voted.
/// </summary>
public sealed class CommunityPoll
{
    /// <summary>
    /// Gets or sets the unique identifier for the poll.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poll question.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short description or context for the poll.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of poll options.
    /// </summary>
    public List<PollOption> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the poll is still active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the category tag for the poll.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the current user has already voted.
    /// </summary>
    public bool HasVoted { get; set; }

    /// <summary>
    /// Gets or sets the ID of the option the current user voted for.
    /// </summary>
    public int? VotedOptionId { get; set; }

    /// <summary>
    /// Gets or sets the poll's shown Comments, oldest first.
    /// </summary>
    public List<CommunityComment> Comments { get; set; } = new();
}

/// <summary>
/// An option of a Poll, with its share of the votes once the viewer has voted.
/// </summary>
public sealed class PollOption
{
    /// <summary>
    /// Gets or sets the database ID of the option.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the option text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets its share of the votes as a whole percentage, or null until the viewer has voted.
    /// </summary>
    public int? Percentage { get; set; }
}

/// <summary>
/// A Strategy pinned on the board.
/// </summary>
public sealed class CommunityStrategy
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the category, as stored (English).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the practice's name.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the author tried.
    /// </summary>
    public string Tried { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public string Happened { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the author keeps doing.
    /// </summary>
    public string Keeping { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author's Pseudonym, or null when the author is a Former member (purged).
    /// </summary>
    public string? AuthorPseudonym { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer wrote this Strategy, so a re-rolled Pseudonym is shown on it at once.
    /// </summary>
    public bool IsOwn { get; set; }

    /// <summary>
    /// Gets or sets where it is in the Admin's review; only the viewer's own can be Pending or Rejected.
    /// </summary>
    public CommunityStrategyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was pinned.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets how many Accounts found it Helpful.
    /// </summary>
    public int HelpfulCount { get; set; }

    /// <summary>
    /// Gets or sets how many Accounts are Cheering its author on.
    /// </summary>
    public int CheeringCount { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer reacted Helpful.
    /// </summary>
    public bool HelpfulByViewer { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer reacted Cheering you on.
    /// </summary>
    public bool CheeringByViewer { get; set; }

    /// <summary>
    /// Gets or sets its shown Comments, oldest first; empty unless it is Approved.
    /// </summary>
    public List<CommunityComment> Comments { get; set; } = new();
}

/// <summary>
/// Represents a community comment on a poll or Strategy.
/// </summary>
public sealed class CommunityComment
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the author's Pseudonym, or null when the author is a Former member (purged).
    /// </summary>
    public string? AuthorPseudonym { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer wrote this comment, so a re-rolled Pseudonym is shown on it at once.
    /// </summary>
    public bool IsOwn { get; set; }

    /// <summary>
    /// Gets or sets the comment text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the comment.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer reacted Helpful. A Comment shows no counts.
    /// </summary>
    public bool HelpfulByViewer { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer reacted Cheering you on.
    /// </summary>
    public bool CheeringByViewer { get; set; }
}

/// <summary>
/// A Challenge on the Guild Hall's rail, running or about to start. The streak and whether the viewer checked in are
/// the viewer's own; of others it shows only how many joined and how many kept it this period.
/// </summary>
public sealed class CommunityChallenge
{
    /// <summary>The most lanterns the card draws; a bigger group lights them in proportion.</summary>
    public const int MaxLanterns = 30;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets its short name.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the habit it asks for.
    /// </summary>
    public string Habit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how often it asks for a Check-in.
    /// </summary>
    public CommunityChallengePeriod Period { get; set; }

    /// <summary>
    /// Gets or sets its first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets its last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets whether it has started in the viewer's time zone.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer joined it.
    /// </summary>
    public bool IsMember { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer checked in for the current period.
    /// </summary>
    public bool CheckedInThisPeriod { get; set; }

    /// <summary>
    /// Gets or sets how many periods in a row the viewer kept; only the viewer sees it.
    /// </summary>
    public int Streak { get; set; }

    /// <summary>
    /// Gets or sets how many Accounts checked in for the current period.
    /// </summary>
    public int CheckedInCount { get; set; }

    /// <summary>
    /// Gets or sets how many Accounts joined it.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// How many lanterns the card draws: one per member, up to <see cref="MaxLanterns"/>.
    /// </summary>
    public int Lanterns => Math.Min(MemberCount, MaxLanterns);

    /// <summary>
    /// How many of the <see cref="Lanterns"/> are lit: the share that checked in this period, at least one when anyone
    /// did.
    /// </summary>
    public int LitLanterns => MemberCount == 0 || CheckedInCount == 0
        ? 0
        : Math.Clamp((int)Math.Round((double)CheckedInCount * Lanterns / MemberCount), 1, Lanterns);
}
