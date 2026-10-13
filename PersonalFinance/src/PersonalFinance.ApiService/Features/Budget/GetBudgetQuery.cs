using MediatR;
using PersonalFinance.ApiService.Services;

namespace PersonalFinance.ApiService.Features.Budget;

/// <summary>
/// Query to get budget allocation for a user and period.
/// </summary>
public sealed class GetBudgetQuery : IRequest<BudgetAllocationResponseDto?>
{
    public string UserId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}

/// <summary>
/// Handler for GetBudgetQuery.
/// </summary>
public sealed class GetBudgetQueryHandler : IRequestHandler<GetBudgetQuery, BudgetAllocationResponseDto?>
{
    private readonly IBudgetService _budgetService;

    public GetBudgetQueryHandler(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    public async Task<BudgetAllocationResponseDto?> Handle(GetBudgetQuery request, CancellationToken cancellationToken)
    {
        return await _budgetService.GetBudgetAsync(request.UserId, request.Period, cancellationToken);
    }
}
