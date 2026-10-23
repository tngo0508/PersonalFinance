namespace PersonalFinance.Shared.DTOs.Todos;

/// <summary>
/// A request to edit a To-do's title and due date. Both are replaced: a <c>null</c> due date makes the To-do
/// undated. The title is trimmed before it is validated and stored, as when adding.
/// </summary>
public sealed class UpdateTodoRequest
{
    /// <summary>Gets or sets the new title (required, at most 200 characters once trimmed).</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the new due date (<c>yyyy-MM-dd</c> in JSON), or <c>null</c> to clear it.</summary>
    public DateOnly? DueDate { get; set; }
}
