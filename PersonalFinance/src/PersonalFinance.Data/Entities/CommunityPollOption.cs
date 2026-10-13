namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity representing a single option within a community poll.
/// </summary>
public class CommunityPollOption
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display text of the option.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display order of the option within the poll.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the parent poll (nullable for cascade safety).
    /// </summary>
    public int? PollId { get; set; }

    /// <summary>
    /// Navigation: the parent poll.
    /// </summary>
    public CommunityPoll? Poll { get; set; }

    /// <summary>
    /// Navigation: all votes for this option.
    /// </summary>
    public ICollection<CommunityPollVote> Votes { get; set; } = new List<CommunityPollVote>();
}
