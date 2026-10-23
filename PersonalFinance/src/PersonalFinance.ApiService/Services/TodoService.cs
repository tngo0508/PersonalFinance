using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Todos;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Reads and writes an Account's To-dos. Soft-deleted To-dos are hidden by the global query filter.
/// </summary>
public class TodoService : ITodoService
{
    private static readonly Expression<Func<TodoItem, TodoItemDto>> ToDto = t => new TodoItemDto
    {
        Id = t.Id,
        Title = t.Title,
        DueDate = t.DueDate,
        CompletedAt = t.CompletedAt,
        CreatedAt = t.CreatedAt
    };

    private static readonly Func<TodoItem, TodoItemDto> MapToDto = ToDto.Compile();

    private readonly AppDbContext _db;

    public TodoService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<List<TodoItemDto>> GetOpenTodosAsync(string userId, CancellationToken cancellationToken = default) =>
        OpenTodosQuery(userId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<TodoItemDto?> GetTodoAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        _db.TodoItems
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<TodoItemDto> CreateTodoAsync(string userId, CreateTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        var todo = new TodoItem
        {
            UserId = userId,
            Title = (request.Title ?? string.Empty).Trim(),
            DueDate = request.DueDate
        };

        _db.TodoItems.Add(todo);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(todo);
    }

    /// <inheritdoc />
    public async Task<TodoItemDto?> UpdateTodoAsync(string userId, int id, UpdateTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        var todo = await _db.TodoItems
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, cancellationToken);
        if (todo is null)
        {
            return null;
        }

        todo.Title = (request.Title ?? string.Empty).Trim();
        todo.DueDate = request.DueDate;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(todo);
    }

    /// <summary>
    /// The open-To-do list query: due date ascending with undated To-dos last, then creation time, then id, so the
    /// order is deterministic. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<TodoItemDto> OpenTodosQuery(string userId) =>
        _db.TodoItems
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.CompletedAt == null)
            .OrderBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(ToDto);
}
