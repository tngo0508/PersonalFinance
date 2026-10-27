namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a community comment on a poll or story. The author is the Account in
/// <see cref="AuditableEntity.UserId"/>, shown by its <see cref="CommunityPseudonym"/>; no name is stored here.
/// </summary>
public class CommunityComment : AuditableEntity
{
    /// <summary>
    /// Gets or sets the comment text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional poll ID if this comment is on a poll.
    /// </summary>
    public int? PollId { get; set; }

    /// <summary>
    /// Gets or sets the optional story ID if this comment is on a story.
    /// </summary>
    public int? StoryId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the comment is approved and visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the time the comment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: the poll this comment belongs to, if any.
    /// </summary>
    public CommunityPoll? Poll { get; set; }

    /// <summary>
    /// Navigation: the story this comment belongs to, if any.
    /// </summary>
    public CommunityStory? Story { get; set; }

    /// <summary>
    /// Navigation: all likes on this comment.
    /// </summary>
    public ICollection<CommunityCommentLike> LikesList { get; set; } = new List<CommunityCommentLike>();
}
