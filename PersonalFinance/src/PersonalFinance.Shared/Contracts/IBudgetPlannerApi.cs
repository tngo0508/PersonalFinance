using Refit;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Budget Planner (yearly allocation per category).
/// </summary>
public interface IBudgetPlannerApi
{
    /// <summary>
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [Get("/api/Budget/budget/{period}")]
    Task<BudgetResponseDto> GetBudgetAsync(string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the current user's budget allocation for a period.
    /// </summary>
    [Post("/api/Budget/budget")]
    Task<SaveBudgetResponseDto> SaveBudgetAsync([Body] SaveBudgetRequestDto request,
        CancellationToken cancellationToken = default);
}
