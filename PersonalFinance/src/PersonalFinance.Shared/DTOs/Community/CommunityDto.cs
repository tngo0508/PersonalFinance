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
    public int? StoryId { get; set; }
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
/// DTO for returning story data to the view/API. Authors are Pseudonyms only (ADR 0018).
/// </summary>
public sealed class CommunityStoryDto
{
    public int Id { get; set; }

    /// <summary>The author's Pseudonym, or null once the author is a Former member (purged, ADR 0005).</summary>
    public string? AuthorPseudonym { get; set; }

    /// <summary>Whether the viewing Account wrote it, so the page can show a re-rolled Pseudonym on it at once.</summary>
    public bool IsOwn { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Likes { get; set; }
    public int CommentCount { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public int ReadTimeMinutes { get; set; }
    public bool IsLikedByUser { get; set; }
}

/// <summary>
/// Result of a story like toggle operation.
/// </summary>
public sealed class StoryLikeResult
{
    public bool Success { get; set; }
    public int NewLikeCount { get; set; }
    public bool IsLiked { get; set; }
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
/// Full Guild Hall response DTO: the viewer's Pseudonym, the polls, stories and comments.
/// </summary>
public sealed class CommunityResponseDto
{
    /// <summary>The viewing Account's Pseudonym, created on its first visit.</summary>
    public string Pseudonym { get; set; } = string.Empty;

    public List<CommunityPollDto> ActivePolls { get; set; } = new();
    public List<CommunityStoryDto> Stories { get; set; } = new();
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
    public int? StoryId { get; set; }
}

/// <summary>
/// Request to submit a story.
/// </summary>
public sealed class CommunityStoryRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
