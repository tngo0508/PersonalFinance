namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for the Community page that displays polls, statistics, and stories.
/// </summary>
public sealed class CommunityViewModel
{
    /// <summary>
    /// Gets or sets any error message to display on the page.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of active polls for community engagement.
    /// </summary>
    public List<CommunityPoll> ActivePolls { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of quick community statistics.
    /// </summary>
    public List<CommunityStat> QuickStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of community stories shared by users.
    /// </summary>
    public List<CommunityStory> Stories { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of recent community comments.
    /// </summary>
    public List<CommunityComment> RecentComments { get; set; } = new();
}

/// <summary>
/// Represents a community poll with open-ended questions.
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
    /// Gets or sets the total number of votes cast.
    /// </summary>
    public int TotalVotes { get; set; }

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
}

/// <summary>
/// Represents a single poll option with its vote count.
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
    /// Gets or sets the number of votes for this option.
    /// </summary>
    public int Votes { get; set; }

    /// <summary>
    /// Gets or sets the percentage of total votes.
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Represents a quick financial statistic or hook to spark discussion.
/// </summary>
public sealed class CommunityStat
{
    /// <summary>
    /// Gets or sets the stat title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric value displayed prominently.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short description or context.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon emoji or SVG identifier.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color theme variant.
    /// </summary>
    public string ThemeColor { get; set; } = string.Empty;
}

/// <summary>
/// Represents a community story shared by a user.
/// </summary>
public sealed class CommunityStory
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the story.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief summary of the story.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full body text of the story.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the story was published.
    /// </summary>
    public DateTime PublishedDate { get; set; }

    /// <summary>
    /// Gets or sets the number of comments on this story.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Gets or sets the category tag.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated reading time in minutes.
    /// </summary>
    public int ReadTimeMinutes { get; set; }

    /// <summary>
    /// Gets or sets the number of likes.
    /// </summary>
    public int Likes { get; set; }

    /// <summary>
    /// Gets or sets whether the story is featured/pinned.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Gets or sets whether the current user has liked this story.
    /// </summary>
    public bool IsLikedByUser { get; set; }
}

/// <summary>
/// Represents a community comment on a poll or story.
/// </summary>
public sealed class CommunityComment
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the author's display name.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comment text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the comment.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the topic or subject the comment relates to.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of likes on the comment.
    /// </summary>
    public int Likes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user liked this comment.
    /// </summary>
    public bool IsLikedByUser { get; set; }

    /// <summary>
    /// Gets or sets the optional poll ID if this comment is on a poll.
    /// </summary>
    public int? PollId { get; set; }

    /// <summary>
    /// Gets or sets the optional story ID if this comment is on a story.
    /// </summary>
    public int? StoryId { get; set; }
}
