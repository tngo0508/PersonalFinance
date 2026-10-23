using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Todos;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC controller for the To-do List page. Signed-out visitors are sent to sign in. The page's JavaScript
/// (<c>js/todos/todo-list.js</c>) calls the JSON actions, which answer with the refreshed open list;
/// <see cref="JsonApiErrorsAttribute"/> turns ApiService failures into <c>{ "error": "..." }</c>.
/// </summary>
[Authorize]
[JsonApiErrors]
public class TodosController : Controller
{
    private readonly ITodoApi _todoApi;
    private readonly ILogger<TodosController> _logger;

    public TodosController(ITodoApi todoApi, ILogger<TodosController> logger)
    {
        _todoApi = todoApi;
        _logger = logger;
    }

    /// <summary>
    /// Shows the To-do List. When the To-dos cannot be loaded, shows the Unavailable State, never the empty state.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        List<TodoItemDto> todos;
        try
        {
            todos = await _todoApi.GetOpenTodosAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load To-dos; rendering the Unavailable State instead.");
            return this.UnavailableView("To-do List", "We couldn't load your To-dos", "Todos");
        }

        return View(new TodoListViewModel { Todos = todos });
    }

    /// <summary>
    /// Adds a To-do and returns the refreshed open list. ApiService validates the title; its 400 message reaches
    /// the page through <see cref="JsonApiErrorsAttribute"/>.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateTodoRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !ModelState.IsValid)
        {
            return BadRequest(new { error = TodoMessages.InvalidRequest });
        }

        await _todoApi.CreateTodoAsync(request, cancellationToken);
        return Json(await _todoApi.GetOpenTodosAsync(cancellationToken));
    }

    /// <summary>
    /// Edits a To-do's title and due date and returns the refreshed open list, re-sorted. ApiService validates the
    /// title and answers 404 for another Account's To-do; both reach the page through
    /// <see cref="JsonApiErrorsAttribute"/>.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTodoRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !ModelState.IsValid)
        {
            return BadRequest(new { error = TodoMessages.InvalidRequest });
        }

        await _todoApi.UpdateTodoAsync(id, request, cancellationToken);
        return Json(await _todoApi.GetOpenTodosAsync(cancellationToken));
    }
}
