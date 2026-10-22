using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Feedback an Account sends to the operator: a Bug, Idea or Other message, with the page it was sent from.
/// Inherits from <see cref="AuditableEntity"/> for the owning Account, soft-delete support and audit fields.
/// </summary>
public class Feedback : AuditableEntity
{
    /// <summary>
    /// Gets or sets the kind of Feedback.
    /// </summary>
    public FeedbackCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the message the Account wrote (at most 2,000 characters).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the app-relative path of the page the Feedback was sent from, if known.
    /// </summary>
    public string? PageUrl { get; set; }

    /// <summary>
    /// Gets or sets where the Feedback is in the Admin's triage.
    /// </summary>
    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;
}
