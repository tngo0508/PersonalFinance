using MediatR;
using PersonalFinance.ApiService.Budget.Services;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Budget;

/// <summary>
/// Command to save a budget allocation.
/// </summary>
public sealed class SaveBudgetCommand : IRequest<Unit>
{
    public string UserId { get; set; } = string.Empty;
    public SaveBudgetRequestDto Request { get; set; } = new();
}

/// <summary>
/// Handler for SaveBudgetCommand.
/// </summary>
public sealed class SaveBudgetCommandHandler : IRequestHandler<SaveBudgetCommand, Unit>
{
    private readonly IBudgetService _budgetService;

    public SaveBudgetCommandHandler(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    public async Task<Unit> Handle(SaveBudgetCommand request, CancellationToken cancellationToken)
    {
        await _budgetService.SaveBudgetAsync(request.UserId, request.Request, cancellationToken);
        return Unit.Value;
    }
}
