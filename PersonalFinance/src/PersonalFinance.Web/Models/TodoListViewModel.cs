using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.Web.Models;

/// <summary>
/// The To-do List page: the Account's open To-dos, in the order ApiService returns them.
/// </summary>
public sealed class TodoListViewModel
{
    /// <summary>Gets the open To-dos: by due date, undated last, then oldest first.</summary>
    public IReadOnlyList<TodoItemDto> Todos { get; init; } = [];
}
