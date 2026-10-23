using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.DTOs.Todos;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for the To-do List. Every endpoint is scoped to the authenticated Account
/// (see XUserIdAuthenticationHandler); another Account's To-do returns 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/todos")]
[Produces("application/json")]
public class TodosController : ControllerBase
{
    private readonly IValidator<CreateTodoRequest> _createValidator;
    private readonly ITodoService _todoService;

    public TodosController(ITodoService todoService, IValidator<CreateTodoRequest> createValidator)
    {
        _todoService = todoService;
        _createValidator = createValidator;
    }

    /// <summary>
    /// Lists the current Account's open To-dos: by due date, undated last, then oldest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TodoItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        return Ok(await _todoService.GetOpenTodosAsync(User.GetUserId(), cancellationToken));
    }

    /// <summary>
    /// Gets one of the current Account's To-dos. Unknown, deleted or another Account's To-dos return 404.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        var todo = await _todoService.GetTodoAsync(User.GetUserId(), id, cancellationToken);
        return todo is null ? NotFound() : Ok(todo);
    }

    /// <summary>
    /// Adds an open To-do for the current Account. An invalid title gets 400 with the messages in <c>errors</c>.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TodoItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var todo = await _todoService.CreateTodoAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = todo.Id }, todo);
    }

    /// <summary>
    /// Soft-deletes one of the current Account's To-dos, open or completed. Unknown, already deleted or another
    /// Account's To-dos return 404.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _todoService.DeleteTodoAsync(User.GetUserId(), id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
