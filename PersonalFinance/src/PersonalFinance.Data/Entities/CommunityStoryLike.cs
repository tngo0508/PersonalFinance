namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's like on a community story.
/// </summary>
public class CommunityStoryLike
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who liked the story.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the story.
    /// </summary>
    public int StoryId { get; set; }

    /// <summary>
    /// Gets or sets the time the like was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: the story that was liked.
    /// </summary>
    public CommunityStory? Story { get; set; }
}
