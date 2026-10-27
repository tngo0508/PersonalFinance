namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user-submitted community story or case study. The author is the Account in
/// <see cref="AuditableEntity.UserId"/>, shown by its <see cref="CommunityPseudonym"/>; no name is stored here.
/// </summary>
public class CommunityStory : AuditableEntity
{
    /// <summary>
    /// Gets or sets the title of the story.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief summary shown in the card preview.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full body text of the story.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category tag (e.g., "Budgeting", "Debt", "Investing").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the story is approved and publicly visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the story is featured/pinned.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Gets or sets the date and time the story was published.
    /// </summary>
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the estimated reading time in minutes.
    /// </summary>
    public int ReadTimeMinutes { get; set; }

    /// <summary>
    /// Navigation: all likes on this story.
    /// </summary>
    public ICollection<CommunityStoryLike> LikesList { get; set; } = new List<CommunityStoryLike>();

    /// <summary>
    /// Navigation: all comments on this story.
    /// </summary>
    public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
}
