namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a community poll question.
/// </summary>
public class CommunityPoll
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets a unique machine-readable identifier for the poll.
    /// </summary>
    public string PollCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poll question text.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short description or context for the poll.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category tag (e.g., "Budgeting", "Goals").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the poll is still accepting votes.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time the poll was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: the options for this poll.
    /// </summary>
    public ICollection<CommunityPollOption> Options { get; set; } = new List<CommunityPollOption>();

    /// <summary>
    /// Navigation: all votes cast on this poll.
    /// </summary>
    public ICollection<CommunityPollVote> Votes { get; set; } = new List<CommunityPollVote>();
}
