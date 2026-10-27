using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.ApiService.Features.BudgetPlanner;

/// <summary>
/// Service for managing user budget allocations.
/// </summary>
public interface IBudgetService
{
    /// <summary>
    /// Gets the budget allocation for a user and period.
    /// </summary>
    Task<BudgetAllocationResponseDto?> GetBudgetAsync(string userId, string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a budget allocation for a user.
    /// </summary>
    Task SaveBudgetAsync(string userId, SaveBudgetRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response DTO for budget allocation.
/// </summary>
public sealed class BudgetAllocationResponseDto
{
    /// <summary>The ISO 4217 currency of the income and every Allocation Amount (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    public BudgetAllocationsDto? Allocations { get; set; }
    public decimal MonthlyIncome { get; set; }
}
