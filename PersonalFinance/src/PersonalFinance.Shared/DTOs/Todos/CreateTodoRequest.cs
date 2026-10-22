namespace PersonalFinance.Shared.DTOs.Todos;

/// <summary>
/// A request to add a To-do. The title is trimmed before it is validated and stored.
/// </summary>
public sealed class CreateTodoRequest
{
    /// <summary>Gets or sets the title (required, at most 200 characters once trimmed).</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the optional due date (<c>yyyy-MM-dd</c> in JSON).</summary>
    public DateOnly? DueDate { get; set; }
}
