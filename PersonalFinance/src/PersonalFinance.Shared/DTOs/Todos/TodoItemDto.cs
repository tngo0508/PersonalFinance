namespace PersonalFinance.Shared.DTOs.Todos;

/// <summary>
/// A To-do as the Account sees it.
/// </summary>
public sealed class TodoItemDto
{
    /// <summary>Gets or sets the To-do id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the trimmed title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional due date (a calendar date, no time or time zone).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Gets or sets when the To-do was marked done (UTC); <c>null</c> while it is open.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Gets or sets when the To-do was added (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
