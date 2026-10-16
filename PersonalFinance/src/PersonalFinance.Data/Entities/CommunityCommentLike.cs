namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's like on a community comment.
/// </summary>
public class CommunityCommentLike : AuditableEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the comment.
    /// </summary>
    public int CommentId { get; set; }

    /// <summary>
    /// Gets or sets the time the like was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: the comment that was liked.
    /// </summary>
    public CommunityComment? Comment { get; set; }
}
