namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a user's vote on a poll option.
/// </summary>
public class CommunityPollVote : AuditableEntity
{
    /// <summary>
    /// Gets or sets the foreign key to the poll option (nullable for cascade safety).
    /// </summary>
    public int? OptionId { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the poll (nullable for cascade safety).
    /// </summary>
    public int? PollId { get; set; }

    /// <summary>
    /// Gets or sets the time the vote was cast.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: the poll option that was voted for.
    /// </summary>
    public CommunityPollOption? Option { get; set; }

    /// <summary>
    /// Navigation: the parent poll.
    /// </summary>
    public CommunityPoll? Poll { get; set; }
}
