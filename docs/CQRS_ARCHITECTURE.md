# CQRS Architecture Guide

This guide explains the Command Query Responsibility Segregation (CQRS) pattern as implemented in the Personal Finance App using MediatR.

## What is CQRS?

CQRS is an architectural pattern that separates read operations (Queries) from write operations (Commands). Instead of having a single service handle both reading and writing data, CQRS splits these concerns:

- **Commands** — Operations that change state (Create, Update, Delete). They return a result indicating success or failure.
- **Queries** — Operations that read data without modifying it. They return data to the caller.

### Why Use CQRS?

- **Clear Separation of Concerns**: Each handler does one thing — either reads or writes.
- **Scalability**: Reads and writes can be scaled independently.
- **Maintainability**: Business logic lives in handlers, not in controllers, keeping controllers thin.
- **Testability**: Handlers can be unit-tested in isolation without the HTTP layer.

## How It Works in This Project

The project uses **MediatR**, a popular .NET library that implements the in-process mediator pattern. MediatR routes Commands and Queries to their corresponding Handlers automatically.

### Request Flow

```
HTTP Request → Controller → IMediator.Send() → Handler → Service → Database
                                      ↑              ↓
                                 Command/Query    Business Logic
```

1. **Controller** receives the HTTP request and extracts parameters.
2. **Controller** creates a Command or Query object with the request data.
3. **Controller** calls `_mediator.Send(command/query)` to dispatch it.
4. **MediatR** finds the matching Handler and invokes it.
5. **Handler** delegates to the appropriate Service for business logic.
6. **Service** performs database operations via EF Core.
7. The result flows back through the same chain to the HTTP response.

## Project Structure

The CQRS components live in `PersonalFinance.ApiService/Features/`, organized by feature:

```
PersonalFinance.ApiService/
├── Controllers/
│   ├── CommunityController.cs          # Uses MediatR for community APIs
│   ├── PersonalFinanceController.cs    # Uses MediatR for budget, savings, watchlist APIs
│   └── NetWorthController.cs           # Direct service calls (non-CQRS)
├── Features/
│   ├── Budget/
│   │   ├── SaveBudgetCommand.cs        # Command + Handler for saving budget
│   │   └── GetBudgetQuery.cs           # Query + Handler for getting budget
│   ├── Community/
│   │   └── CommunityCommands.cs        # Multiple commands/queries for community features
│   ├── SavingsGoals/
│   │   └── SavingsGoalCommands.cs      # CRUD commands + query for savings goals
│   └── Watchlist/
│       └── WatchlistCommands.cs        # Commands for saving/removing, queries for listing
└── Services/
    ├── IBudgetService.cs / BudgetService.cs
    ├── ICommunityService.cs / CommunityService.cs
    ├── ISavingsGoalService.cs / SavingsGoalService.cs
    └── IWatchlistService.cs / WatchlistService.cs
```

## Component Breakdown

### 1. Commands and Queries

Commands and Queries are simple data classes that implement `IRequest<TResponse>` from MediatR.

**Command Example** (write operation):
```csharp
public sealed class CreateSavingsGoalCommand : IRequest<int>
{
    public string UserId { get; set; } = string.Empty;
    public CreateSavingsGoalRequestDto Request { get; set; } = new();
}
```

**Query Example** (read operation):
```csharp
public sealed class GetSavingsGoalsQuery : IRequest<List<SavingsGoalResponseDto>>
{
    public string UserId { get; set; } = string.Empty;
}
```

**Key Points:**
- Commands implement `IRequest<T>` where `T` is the return type (e.g., `int`, `bool`, DTO, or `Unit` for void).
- Queries implement `IRequest<T>` where `T` is the data being returned.
- `Unit` is a MediatR type used when a command doesn't return a meaningful value.
- Properties on Commands/Queries carry the input data needed by the handler.

### 2. Handlers

Handlers contain the orchestration logic. They implement `IRequestHandler<TRequest, TResponse>`.

**Handler Example:**
```csharp
public sealed class CreateSavingsGoalCommandHandler : IRequestHandler<CreateSavingsGoalCommand, int>
{
    private readonly ISavingsGoalService _service;

    public CreateSavingsGoalCommandHandler(ISavingsGoalService service)
    {
        _service = service;
    }

    public async Task<int> Handle(CreateSavingsGoalCommand request, CancellationToken cancellationToken)
    {
        return await _service.CreateSavingsGoalAsync(request.UserId, request.Request, cancellationToken);
    }
}
```

**Key Points:**
- Handlers are injected with the services they need via constructor injection.
- The `Handle` method receives the Command/Query and a `CancellationToken`.
- Handlers delegate to services — they don't contain database logic directly.
- Each handler is focused on one use case.

### 3. Services

Services contain the actual business logic and database operations.

**Service Interface Example:**
```csharp
public interface ISavingsGoalService
{
    Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);
    Task<List<SavingsGoalResponseDto>> GetSavingsGoalsAsync(string userId, CancellationToken cancellationToken = default);
}
```

Services are registered in `Program.cs`:
```csharp
builder.Services.AddScoped<ISavingsGoalService, SavingsGoalService>();
```

### 4. Controllers

Controllers are thin — they handle HTTP concerns and dispatch to MediatR.

**Controller Example:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class PersonalFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonalFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("savings-goals")]
    public async Task<IActionResult> GetSavingsGoals(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var goals = await _mediator.Send(new GetSavingsGoalsQuery { UserId = userId }, cancellationToken);
        return Ok(goals);
    }

    [HttpPost("savings-goals")]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var id = await _mediator.Send(new CreateSavingsGoalCommand { UserId = userId, Request = request }, cancellationToken);
        return CreatedAtAction(nameof(GetSavingsGoalById), new { id }, id);
    }
}
```

**Key Points:**
- Controllers inject `IMediator`, not individual handlers or services.
- User identification comes from the `X-User-Id` header (forwarded by the Web proxy).
- Controllers map HTTP status codes to results.
- No business logic in controllers.

## MediatR Registration

MediatR is registered once in `PersonalFinance.ApiService/Program.cs`:

```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```

This automatically discovers all Commands, Queries, and Handlers in the assembly — no manual registration needed.

## Existing CQRS Features

| Feature | Commands | Queries | Controller |
|---|---|---|---|
| **Budget** | `SaveBudgetCommand` | `GetBudgetQuery` | `PersonalFinanceController` |
| **Savings Goals** | `CreateSavingsGoalCommand`, `UpdateSavingsGoalCommand`, `DeleteSavingsGoalCommand` | `GetSavingsGoalsQuery` | `PersonalFinanceController` |
| **Watchlist** | `SaveArticleCommand`, `RemoveArticleCommand` | `GetWatchlistQuery`, `CheckArticleSavedQuery` | `PersonalFinanceController` |
| **Community** | `VoteOnPollCommand`, `AddCommentCommand`, `ToggleCommentLikeCommand`, `SubmitStoryCommand`, `ToggleStoryLikeCommand` | `GetCommunityQuery`, `GetPollCommentsQuery`, `GetStoryByIdQuery` | `CommunityController` |

## How to Add a New API Endpoint

Follow these steps to add a new feature using the CQRS pattern:

### Step 1: Define the DTOs

Place request/response DTOs in `PersonalFinance.Shared/DTOs/` or alongside the contract in `PersonalFinance.Shared/Contracts/`.

```csharp
// PersonalFinance.Shared/DTOs/MyFeatureDto.cs
public class CreateMyFeatureRequestDto
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class MyFeatureResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Step 2: Create the Service Interface and Implementation

Add the service interface and implementation in `PersonalFinance.ApiService/Services/`.

```csharp
// PersonalFinance.ApiService/Services/IMyFeatureService.cs
public interface IMyFeatureService
{
    Task<int> CreateAsync(string userId, CreateMyFeatureRequestDto request, CancellationToken cancellationToken = default);
    Task<List<MyFeatureResponseDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
}
```

```csharp
// PersonalFinance.ApiService/Services/MyFeatureService.cs
public class MyFeatureService : IMyFeatureService
{
    private readonly AppDbContext _context;

    public MyFeatureService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAsync(string userId, CreateMyFeatureRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = new MyFeatureEntity
        {
            UserId = userId,
            Title = request.Title,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        };
        _context.MyFeatures.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<List<MyFeatureResponseDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.MyFeatures
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => new MyFeatureResponseDto
            {
                Id = e.Id,
                Title = e.Title,
                Amount = e.Amount,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
```

### Step 3: Register the Service

In `PersonalFinance.ApiService/Program.cs`, add the service registration:

```csharp
builder.Services.AddScoped<IMyFeatureService, MyFeatureService>();
```

### Step 4: Create the Command and Query

Create a new folder under `PersonalFinance.ApiService/Features/` for your feature:

```csharp
// PersonalFinance.ApiService/Features/MyFeature/MyFeatureCommands.cs
using MediatR;
using PersonalFinance.ApiService.Services;

namespace PersonalFinance.ApiService.Features.MyFeature;

public sealed class CreateMyFeatureCommand : IRequest<int>
{
    public string UserId { get; set; } = string.Empty;
    public CreateMyFeatureRequestDto Request { get; set; } = new();
}

public sealed class CreateMyFeatureCommandHandler : IRequestHandler<CreateMyFeatureCommand, int>
{
    private readonly IMyFeatureService _service;

    public CreateMyFeatureCommandHandler(IMyFeatureService service)
    {
        _service = service;
    }

    public async Task<int> Handle(CreateMyFeatureCommand request, CancellationToken cancellationToken)
    {
        return await _service.CreateAsync(request.UserId, request.Request, cancellationToken);
    }
}

public sealed class GetMyFeaturesQuery : IRequest<List<MyFeatureResponseDto>>
{
    public string UserId { get; set; } = string.Empty;
}

public sealed class GetMyFeaturesQueryHandler : IRequestHandler<GetMyFeaturesQuery, List<MyFeatureResponseDto>>
{
    private readonly IMyFeatureService _service;

    public GetMyFeaturesQueryHandler(IMyFeatureService service)
    {
        _service = service;
    }

    public async Task<List<MyFeatureResponseDto>> Handle(GetMyFeaturesQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetAllAsync(request.UserId, cancellationToken);
    }
}
```

### Step 5: Add the Controller Endpoints

Add endpoints to an existing controller or create a new one:

```csharp
// PersonalFinance.ApiService/Controllers/MyFeatureController.cs
[ApiController]
[Route("api/[controller]")]
public class MyFeatureController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyFeatureController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "anonymous";
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var items = await _mediator.Send(new GetMyFeaturesQuery { UserId = userId }, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMyFeatureRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var id = await _mediator.Send(new CreateMyFeatureCommand { UserId = userId, Request = request }, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }
}
```

### Step 6: Verify

- Build the solution to ensure there are no compilation errors.
- Test the endpoints using the Scalar API reference at `/scalar/v1` (available in development).
- Confirm the `X-User-Id` header is properly forwarded from the Web layer.

## Naming Conventions

Follow these naming patterns for consistency:

| Component | Pattern | Example |
|---|---|---|
| Command Class | `{Action}{Feature}Command` | `CreateSavingsGoalCommand` |
| Command Handler | `{Action}{Feature}CommandHandler` | `CreateSavingsGoalCommandHandler` |
| Query Class | `Get{Feature}Query` or `{Feature}Query` | `GetSavingsGoalsQuery` |
| Query Handler | `Get{Feature}QueryHandler` or `{Feature}QueryHandler` | `GetSavingsGoalsQueryHandler` |
| Service Interface | `I{Feature}Service` | `ISavingsGoalService` |
| Service Implementation | `{Feature}Service` | `SavingsGoalService` |
| Request DTO | `{Action}{Feature}RequestDto` | `CreateSavingsGoalRequestDto` |
| Response DTO | `{Feature}ResponseDto` | `SavingsGoalResponseDto` |

## Common Patterns

### Commands That Return `Unit`

When a command doesn't need to return a value, use `Unit`:

```csharp
public sealed class SaveBudgetCommand : IRequest<Unit>
{
    public string UserId { get; set; } = string.Empty;
    public SaveBudgetRequestDto Request { get; set; } = new();
}

public sealed class SaveBudgetCommandHandler : IRequestHandler<SaveBudgetCommand, Unit>
{
    public async Task<Unit> Handle(SaveBudgetCommand request, CancellationToken cancellationToken)
    {
        await _budgetService.SaveBudgetAsync(request.UserId, request.Request, cancellationToken);
        return Unit.Value;
    }
}
```

### Multiple Commands in One File

For related operations, group them in a single file (as seen in `CommunityCommands.cs`):

```csharp
namespace PersonalFinance.ApiService.Features.Community;

public sealed class VoteOnPollCommand : IRequest<CommunityVoteResultDto> { }
public sealed class VoteOnPollCommandHandler : IRequestHandler<VoteOnPollCommand, CommunityVoteResultDto> { }

public sealed class AddCommentCommand : IRequest<CommunityCommentDto> { }
public sealed class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, CommunityCommentDto> { }
```

### Error Handling in Controllers

Catch exceptions at the controller level and return appropriate HTTP status codes:

```csharp
[HttpPost("comment/{commentId}/like")]
public async Task<ActionResult<CommentLikeResult>> ToggleCommentLike(int commentId, CancellationToken cancellationToken = default)
{
    var userId = GetUserId();
    try
    {
        var result = await _mediator.Send(new ToggleCommentLikeCommand { UserId = userId, CommentId = commentId }, cancellationToken);
        return Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return NotFound();
    }
}
```

## Non-CQRS Controllers

Not all controllers use CQRS. For example, `NetWorthController` calls services directly:

```csharp
public class NetWorthController : ControllerBase
{
    private readonly INetWorthParametersService _parametersService;
    private readonly NetWorthProjectionService _projectionService;

    public NetWorthController(INetWorthParametersService parametersService, NetWorthProjectionService projectionService)
    {
        _parametersService = parametersService;
        _projectionService = projectionService;
    }
}
```

This is acceptable for simpler features or when the mediation overhead isn't justified. The CQRS pattern is used where the feature benefits from clear separation of commands and queries.

## References

- [MediatR GitHub Repository](https://github.com/jbogard/MediatR)
- [CQRS Pattern - Microsoft Documentation](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices)
