using Refit;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Personal Finance features: savings goals, budget planner, and article watchlist.
/// </summary>
public interface IPersonalFinanceApi
{
    // =========================================================================
    // Savings Goals
    // =========================================================================

    /// <summary>
    /// Gets all savings goals for the current user.
    /// </summary>
    [Get("/api/PersonalFinance/savings-goals")]
    Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [Post("/api/PersonalFinance/savings-goals")]
    Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync([Body] CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [Put("/api/PersonalFinance/savings-goals/{id}")]
    Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, [Body] UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [Delete("/api/PersonalFinance/savings-goals/{id}")]
    Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id, CancellationToken cancellationToken = default);

    // =========================================================================
    // Watchlist
    // =========================================================================

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [Get("/api/PersonalFinance/watchlist")]
    Task<List<SavedArticleDto>> GetWatchlistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [Post("/api/PersonalFinance/watchlist")]
    Task<SaveArticleResponseDto> SaveArticleAsync([Body] SaveArticleRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [Delete("/api/PersonalFinance/watchlist/{id}")]
    Task<RemoveArticleResponseDto> RemoveArticleAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [Get("/api/PersonalFinance/watchlist/check")]
    Task<CheckArticleSavedResponseDto> CheckArticleSavedAsync([Query] string url, CancellationToken cancellationToken = default);

    // =========================================================================
    // Budget
    // =========================================================================

    /// <summary>
    /// Gets the current user's budget allocation for the specified period.
    /// </summary>
    [Get("/api/PersonalFinance/budget/{period}")]
    Task<BudgetResponseDto> GetBudgetAsync(string period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the current user's budget allocation for a period.
    /// </summary>
    [Post("/api/PersonalFinance/budget")]
    Task<SaveBudgetResponseDto> SaveBudgetAsync([Body] SaveBudgetRequestDto request, CancellationToken cancellationToken = default);
}

// =========================================================================
// DTOs
// =========================================================================

public class SavingsGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public double ProgressPercent { get; set; }
}

public class CreateSavingsGoalRequestDto
{
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
}

public class UpdateSavingsGoalRequestDto
{
    public string? Name { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class CreateSavingsGoalResponseDto
{
    public bool Success { get; set; }
    public int Id { get; set; }
}

public class UpdateSavingsGoalResponseDto
{
    public bool Success { get; set; }
}

public class DeleteSavingsGoalResponseDto
{
    public bool Success { get; set; }
}

public class SavedArticleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SavedAtUtc { get; set; }
}

public class SaveArticleRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

public class SaveArticleResponseDto
{
    public bool Success { get; set; }
    public bool AlreadySaved { get; set; }
    public int Id { get; set; }
}

public class RemoveArticleResponseDto
{
    public bool Success { get; set; }
}

public class CheckArticleSavedResponseDto
{
    public bool IsSaved { get; set; }
}

public class BudgetResponseDto
{
    public object? Allocations { get; set; }
    public decimal MonthlyIncome { get; set; }
}

public class SaveBudgetRequestDto
{
    public string? Period { get; set; }
    public decimal MonthlyIncome { get; set; }
    public BudgetAllocationsDto Allocations { get; set; } = new();
}

public class BudgetAllocationsDto
{
    public decimal Housing { get; set; }
    public decimal Food { get; set; }
    public decimal Transportation { get; set; }
    public decimal Utilities { get; set; }
    public decimal Insurance { get; set; }
    public decimal Healthcare { get; set; }
    public decimal Savings { get; set; }
    public decimal DebtRepayment { get; set; }
    public decimal Entertainment { get; set; }
    public decimal Miscellaneous { get; set; }
}

public class SaveBudgetResponseDto
{
    public bool Success { get; set; }
}
