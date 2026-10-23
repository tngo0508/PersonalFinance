using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Todos;
using TodosApiController = PersonalFinance.ApiService.Controllers.TodosController;

namespace PersonalFinance.Tests.Todos;

/// <summary>
/// The ApiService <c>api/todos</c> endpoints, driven through the controller with the real service and validators on
/// the InMemory provider: an Account adds, edits and lists its open To-dos, and never sees another Account's.
/// </summary>
public class TodosEndpointTests
{
    private const string UserId = "todo-user";

    private static AppDbContext CreateContext(string? name = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"TodosEndpoint_{Guid.NewGuid():N}")
            .Options);

    private static TodosApiController CreateController(AppDbContext db, string userId = UserId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuthType");
        return new TodosApiController(new TodoService(db), new CreateTodoRequestValidator(),
            new UpdateTodoRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static async Task<List<TodoItemDto>> ListAsync(AppDbContext db, string userId = UserId)
    {
        var result = await CreateController(db, userId).List();
        return Assert.IsType<List<TodoItemDto>>(Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Create_ValidTodo_ReturnsItTrimmed_AndListsIt()
    {
        await using var db = CreateContext();

        var result = await CreateController(db).Create(new CreateTodoRequest
        {
            Title = "  Renew car insurance  ", DueDate = new DateOnly(2026, 11, 3)
        });

        var created = Assert.IsType<TodoItemDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal("Renew car insurance", created.Title);
        Assert.Equal(new DateOnly(2026, 11, 3), created.DueDate);
        Assert.Null(created.CompletedAt);
        var listed = Assert.Single(await ListAsync(db));
        Assert.Equal(created.Id, listed.Id);
        Assert.Equal("Renew car insurance", listed.Title);
    }

    [Fact]
    public async Task Create_WithoutADueDate_AddsAnUndatedTodo()
    {
        await using var db = CreateContext();

        await CreateController(db).Create(new CreateTodoRequest { Title = "Call the bank" });

        Assert.Null(Assert.Single(await ListAsync(db)).DueDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public async Task Create_RejectsAMissingOrWhitespaceTitle(string? title)
    {
        await using var db = CreateContext();

        var errors = await AssertBadRequestAsync(db, new CreateTodoRequest { Title = title });

        Assert.Contains("Title is required.", errors);
    }

    [Fact]
    public async Task Create_RejectsATitleOverTheMaximumLength()
    {
        await using var db = CreateContext();

        var errors = await AssertBadRequestAsync(db, new CreateTodoRequest { Title = new string('a', 201) });

        Assert.Contains("Title must be 200 characters or fewer.", errors);
    }

    [Fact]
    public async Task Create_MeasuresTheTitleAfterTrimming()
    {
        await using var db = CreateContext();

        var result = await CreateController(db).Create(new CreateTodoRequest
        {
            Title = $"  {new string('a', 200)}  "
        });

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(200, Assert.Single(await ListAsync(db)).Title.Length);
    }

    [Fact]
    public async Task List_OrdersByDueDate_UndatedLast_ThenByCreationTime_ThenById()
    {
        await using var db = CreateContext();
        var sameTime = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        db.TodoItems.AddRange(
            new TodoItem { Id = 1, UserId = UserId, Title = "Undated, older", CreatedAt = sameTime.AddDays(-1) },
            new TodoItem { Id = 2, UserId = UserId, Title = "Due later", DueDate = new DateOnly(2026, 12, 1), CreatedAt = sameTime },
            new TodoItem { Id = 3, UserId = UserId, Title = "Undated, newer", CreatedAt = sameTime },
            new TodoItem { Id = 4, UserId = UserId, Title = "Due soon, tie B", DueDate = new DateOnly(2026, 11, 1), CreatedAt = sameTime },
            new TodoItem { Id = 5, UserId = UserId, Title = "Due soon, tie A", DueDate = new DateOnly(2026, 11, 1), CreatedAt = sameTime },
            new TodoItem { Id = 6, UserId = UserId, Title = "Due soon, oldest", DueDate = new DateOnly(2026, 11, 1), CreatedAt = sameTime.AddHours(-1) });
        await db.SaveChangesAsync();

        var titles = (await ListAsync(db)).Select(t => t.Title);

        Assert.Equal(
            ["Due soon, oldest", "Due soon, tie B", "Due soon, tie A", "Due later", "Undated, older", "Undated, newer"],
            titles);
    }

    [Fact]
    public async Task List_LeavesOutCompletedAndDeletedTodos()
    {
        await using var db = CreateContext();
        var deleted = new TodoItem { UserId = UserId, Title = "Deleted" };
        deleted.SoftDelete();
        db.TodoItems.AddRange(
            new TodoItem { UserId = UserId, Title = "Open" },
            new TodoItem { UserId = UserId, Title = "Done", CompletedAt = DateTime.UtcNow },
            deleted);
        await db.SaveChangesAsync();

        Assert.Equal("Open", Assert.Single(await ListAsync(db)).Title);
    }

    [Fact]
    public async Task List_ShowsOnlyTheAccountsOwnTodos()
    {
        await using var db = CreateContext();
        await CreateController(db, "other-user").Create(new CreateTodoRequest { Title = "Someone else's" });
        await CreateController(db).Create(new CreateTodoRequest { Title = "Mine" });

        Assert.Equal("Mine", Assert.Single(await ListAsync(db)).Title);
    }

    [Fact]
    public async Task Get_ReturnsTheAccountsOwnTodo()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Mine");

        var result = await CreateController(db).Get(created.Id);

        Assert.Equal("Mine", Assert.IsType<TodoItemDto>(Assert.IsType<OkObjectResult>(result).Value).Title);
    }

    [Fact]
    public async Task Get_AnotherAccountsTodo_Returns404()
    {
        await using var db = CreateContext();
        var theirs = await CreateAsync(db, "Someone else's", "other-user");

        var result = await CreateController(db).Get(theirs.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_SoftDeletesTheTodo_AndItLeavesTheList()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Cancel the gym");
        await CreateAsync(db, "Keep me");

        var result = await CreateController(db).Delete(created.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Keep me", Assert.Single(await ListAsync(db)).Title);
        Assert.IsType<NotFoundResult>(await CreateController(db).Get(created.Id));
        var row = await db.TodoItems.IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Delete_ACompletedTodo_SoftDeletesIt()
    {
        await using var db = CreateContext();
        var done = new TodoItem { UserId = UserId, Title = "Done", CompletedAt = DateTime.UtcNow };
        db.TodoItems.Add(done);
        await db.SaveChangesAsync();

        var result = await CreateController(db).Delete(done.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.IsType<NotFoundResult>(await CreateController(db).Get(done.Id));
    }

    [Fact]
    public async Task Delete_AnotherAccountsTodo_Returns404_AndKeepsIt()
    {
        await using var db = CreateContext();
        var theirs = await CreateAsync(db, "Someone else's", "other-user");

        var result = await CreateController(db).Delete(theirs.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("Someone else's", Assert.Single(await ListAsync(db, "other-user")).Title);
    }

    [Fact]
    public async Task Delete_AnAlreadyDeletedOrUnknownTodo_Returns404()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Once");
        await CreateController(db).Delete(created.Id);

        Assert.IsType<NotFoundResult>(await CreateController(db).Delete(created.Id));
        Assert.IsType<NotFoundResult>(await CreateController(db).Delete(999));
    }

    [Fact]
    public async Task Update_ChangesTheTitleAndDueDate_Trimmed()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Call the bank");

        var result = await CreateController(db).Update(created.Id, new UpdateTodoRequest
        {
            Title = "  Call the bank about the loan  ", DueDate = new DateOnly(2026, 11, 5)
        });

        var updated = Assert.IsType<TodoItemDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Call the bank about the loan", updated.Title);
        Assert.Equal(new DateOnly(2026, 11, 5), updated.DueDate);
        var listed = Assert.Single(await ListAsync(db));
        Assert.Equal("Call the bank about the loan", listed.Title);
        Assert.Equal(new DateOnly(2026, 11, 5), listed.DueDate);
    }

    [Fact]
    public async Task Update_ANewDueDate_ResortsTheList()
    {
        await using var db = CreateContext();
        var first = await CreateAsync(db, "First", dueDate: new DateOnly(2026, 11, 1));
        await CreateAsync(db, "Second", dueDate: new DateOnly(2026, 11, 2));

        await CreateController(db).Update(first.Id, new UpdateTodoRequest
        {
            Title = "First", DueDate = new DateOnly(2026, 11, 3)
        });

        Assert.Equal(["Second", "First"], (await ListAsync(db)).Select(t => t.Title));
    }

    [Fact]
    public async Task Update_ClearingTheDueDate_MakesTheTodoUndated_AndSortsItLast()
    {
        await using var db = CreateContext();
        var dated = await CreateAsync(db, "Was dated", dueDate: new DateOnly(2026, 11, 1));
        await CreateAsync(db, "Still dated", dueDate: new DateOnly(2026, 12, 1));

        await CreateController(db).Update(dated.Id, new UpdateTodoRequest { Title = "Was dated", DueDate = null });

        var todos = await ListAsync(db);
        Assert.Equal(["Still dated", "Was dated"], todos.Select(t => t.Title));
        Assert.Null(todos[^1].DueDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public async Task Update_RejectsAMissingOrWhitespaceTitle_AndKeepsTheTodo(string? title)
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Keep me");

        var errors = await AssertUpdateBadRequestAsync(db, created.Id, new UpdateTodoRequest { Title = title });

        Assert.Contains("Title is required.", errors);
        Assert.Equal("Keep me", Assert.Single(await ListAsync(db)).Title);
    }

    [Fact]
    public async Task Update_RejectsATitleOverTheMaximumLength()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Keep me");

        var errors = await AssertUpdateBadRequestAsync(db, created.Id,
            new UpdateTodoRequest { Title = new string('a', 201) });

        Assert.Contains("Title must be 200 characters or fewer.", errors);
        Assert.Equal("Keep me", Assert.Single(await ListAsync(db)).Title);
    }

    [Fact]
    public async Task Update_MeasuresTheTitleAfterTrimming()
    {
        await using var db = CreateContext();
        var created = await CreateAsync(db, "Short");

        var result = await CreateController(db).Update(created.Id,
            new UpdateTodoRequest { Title = $"  {new string('a', 200)}  " });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, Assert.Single(await ListAsync(db)).Title.Length);
    }

    [Fact]
    public async Task Update_AnotherAccountsTodo_Returns404_AndLeavesItUnchanged()
    {
        await using var db = CreateContext();
        var theirs = await CreateAsync(db, "Someone else's", "other-user");

        var result = await CreateController(db).Update(theirs.Id, new UpdateTodoRequest { Title = "Hijacked" });

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("Someone else's", Assert.Single(await ListAsync(db, "other-user")).Title);
    }

    [Fact]
    public async Task Update_AnUnknownOrDeletedTodo_Returns404()
    {
        await using var db = CreateContext();
        var deleted = new TodoItem { UserId = UserId, Title = "Deleted" };
        deleted.SoftDelete();
        db.TodoItems.Add(deleted);
        await db.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await CreateController(db).Update(deleted.Id, new UpdateTodoRequest { Title = "Back" }));
        Assert.IsType<NotFoundResult>(await CreateController(db).Update(9999, new UpdateTodoRequest { Title = "Nope" }));
    }

    private static async Task<IEnumerable<string>> AssertUpdateBadRequestAsync(AppDbContext db, int id,
        UpdateTodoRequest request)
    {
        var result = await CreateController(db).Update(id, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errors = badRequest.Value!.GetType().GetProperty("errors")!.GetValue(badRequest.Value);
        return Assert.IsAssignableFrom<IEnumerable<string>>(errors);
    }

    private static async Task<TodoItemDto> CreateAsync(AppDbContext db, string title, string userId = UserId,
        DateOnly? dueDate = null)
    {
        var result = await CreateController(db, userId).Create(new CreateTodoRequest { Title = title, DueDate = dueDate });
        return Assert.IsType<TodoItemDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
    }

    private static async Task<IEnumerable<string>> AssertBadRequestAsync(AppDbContext db, CreateTodoRequest request)
    {
        var result = await CreateController(db).Create(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.TodoItems.ToListAsync());
        var errors = badRequest.Value!.GetType().GetProperty("errors")!.GetValue(badRequest.Value);
        return Assert.IsAssignableFrom<IEnumerable<string>>(errors);
    }
}
