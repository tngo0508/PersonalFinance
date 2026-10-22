namespace PersonalFinance.Data.Entities;

/// <summary>
/// A To-do: a short item an Account wants to get done, with an optional due date. Open while
/// <see cref="CompletedAt"/> is <c>null</c>. Inherits from <see cref="AuditableEntity"/> for the owning Account,
/// soft-delete support and audit fields.
/// </summary>
public class TodoItem : AuditableEntity
{
    /// <summary>
    /// Gets or sets the trimmed title (at most 200 characters).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional due date: a calendar date with no time or time zone.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Gets or sets when the To-do was marked done (UTC); <c>null</c> while it is open.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
