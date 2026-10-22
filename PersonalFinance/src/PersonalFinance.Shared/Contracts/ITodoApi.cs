using Refit;
using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the To-do List. Calls are made as the signed-in Account.
/// </summary>
public interface ITodoApi
{
    /// <summary>
    /// Lists the Account's open To-dos: by due date, undated last, then oldest first.
    /// </summary>
    [Get("/api/todos")]
    Task<List<TodoItemDto>> GetOpenTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one of the Account's To-dos; another Account's id fails with 404.
    /// </summary>
    [Get("/api/todos/{id}")]
    Task<TodoItemDto> GetTodoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a To-do. An invalid title fails with 400 and the messages in <c>errors</c>.
    /// </summary>
    [Post("/api/todos")]
    Task<TodoItemDto> CreateTodoAsync([Body] CreateTodoRequest request, CancellationToken cancellationToken = default);
}
