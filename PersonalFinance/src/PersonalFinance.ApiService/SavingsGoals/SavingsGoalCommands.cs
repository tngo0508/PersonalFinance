using MediatR;
using PersonalFinance.ApiService.SavingsGoals.Services;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.SavingsGoals;

/// <summary>
/// Command to create a savings goal.
/// </summary>
public sealed class CreateSavingsGoalCommand : IRequest<int>
{
    public string UserId { get; set; } = string.Empty;
    public CreateSavingsGoalRequestDto Request { get; set; } = new();
}

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

/// <summary>
/// Command to update a savings goal.
/// </summary>
public sealed class UpdateSavingsGoalCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public int Id { get; set; }
    public UpdateSavingsGoalRequestDto Request { get; set; } = new();
}

public sealed class UpdateSavingsGoalCommandHandler : IRequestHandler<UpdateSavingsGoalCommand, bool>
{
    private readonly ISavingsGoalService _service;

    public UpdateSavingsGoalCommandHandler(ISavingsGoalService service)
    {
        _service = service;
    }

    public async Task<bool> Handle(UpdateSavingsGoalCommand request, CancellationToken cancellationToken)
    {
        return await _service.UpdateSavingsGoalAsync(request.UserId, request.Id, request.Request, cancellationToken);
    }
}

/// <summary>
/// Command to delete a savings goal.
/// </summary>
public sealed class DeleteSavingsGoalCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public int Id { get; set; }
}

public sealed class DeleteSavingsGoalCommandHandler : IRequestHandler<DeleteSavingsGoalCommand, bool>
{
    private readonly ISavingsGoalService _service;

    public DeleteSavingsGoalCommandHandler(ISavingsGoalService service)
    {
        _service = service;
    }

    public async Task<bool> Handle(DeleteSavingsGoalCommand request, CancellationToken cancellationToken)
    {
        return await _service.DeleteSavingsGoalAsync(request.UserId, request.Id, cancellationToken);
    }
}

/// <summary>
/// Query to get all savings goals for a user.
/// </summary>
public sealed class GetSavingsGoalsQuery : IRequest<List<SavingsGoalResponseDto>>
{
    public string UserId { get; set; } = string.Empty;
}

public sealed class GetSavingsGoalsQueryHandler : IRequestHandler<GetSavingsGoalsQuery, List<SavingsGoalResponseDto>>
{
    private readonly ISavingsGoalService _service;

    public GetSavingsGoalsQueryHandler(ISavingsGoalService service)
    {
        _service = service;
    }

    public async Task<List<SavingsGoalResponseDto>> Handle(GetSavingsGoalsQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetSavingsGoalsAsync(request.UserId, cancellationToken);
    }
}
