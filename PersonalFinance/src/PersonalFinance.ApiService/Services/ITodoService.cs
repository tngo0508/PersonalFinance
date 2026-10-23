using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// An Account's To-dos. Every method is scoped to <c>userId</c>: another Account's To-do is treated as missing.
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// Lists the Account's open To-dos: by due date, undated last, then oldest first (id breaks ties).
    /// </summary>
    Task<List<TodoItemDto>> GetOpenTodosAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one of the Account's To-dos, or <c>null</c> when it does not exist, is deleted or belongs to another
    /// Account.
    /// </summary>
    Task<TodoItemDto?> GetTodoAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an open To-do for the Account with the trimmed title. The request must already be valid.
    /// </summary>
    Task<TodoItemDto> CreateTodoAsync(string userId, CreateTodoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the title (trimmed) and due date of one of the Account's To-dos; a <c>null</c> due date makes it
    /// undated. Returns <c>null</c> when the To-do does not exist, is deleted or belongs to another Account. The
    /// request must already be valid.
    /// </summary>
    Task<TodoItemDto?> UpdateTodoAsync(string userId, int id, UpdateTodoRequest request,
        CancellationToken cancellationToken = default);
}
