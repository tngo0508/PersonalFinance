using PersonalFinance.Shared.Enums;
using Refit;

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
    [Get("/api/SavingsGoals")]
    Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [Post("/api/SavingsGoals")]
    Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync([Body] CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [Put("/api/SavingsGoals/{id}")]
    Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, [Body] UpdateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [Delete("/api/SavingsGoals/{id}")]
    Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id, CancellationToken cancellationToken = default);

    // =========================================================================
    // Watchlist
    // =========================================================================

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [Get("/api/Watchlist")]
    Task<List<SavedArticleDto>> GetWatchlistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [Post("/api/Watchlist")]
    Task<SaveArticleResponseDto> SaveArticleAsync([Body] SaveArticleRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [Delete("/api/Watchlist/{id}")]
    Task<RemoveArticleResponseDto> RemoveArticleAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [Get("/api/Watchlist/check")]
    Task<CheckArticleSavedResponseDto> CheckArticleSavedAsync([Query] string url,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // Budget
    // =========================================================================

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

    // =========================================================================
    // Spreadsheet Analysis
    // =========================================================================

    /// <summary>
    ///     Gets the full analysis dashboard data for a selected period.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/dashboard")]
    Task<AnalysisDashboardDto> GetAnalysisDashboardAsync(
        [Query] int? year = null,
        [Query] int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Syncs budget data from a Google Drive spreadsheet to the database.
    /// </summary>
    [Post("/api/SpreadsheetAnalysis/sync")]
    Task<SyncSpreadsheetResponseDto> SyncSpreadsheetAsync(
        [Body] SyncSpreadsheetRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets available years for the current user.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/years")]
    Task<List<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets available months for a given year.
    /// </summary>
    [Get("/api/SpreadsheetAnalysis/months/{year}")]
    Task<List<int>> GetAvailableMonthsAsync(int year, CancellationToken cancellationToken = default);
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
    public BudgetAllocationsDto? Allocations { get; set; }
    public decimal MonthlyIncome { get; set; }
}

public class SaveBudgetRequestDto
{
    public string? Period { get; set; }
    public decimal MonthlyIncome { get; set; }
    public BudgetAllocationsDto Allocations { get; set; } = new();
}

/// <summary>
/// Dollar amounts allocated to each budget category.
/// Each value represents the exact amount in dollars and cents, not a percentage.
/// </summary>
public class BudgetAllocationsDto
{
    /// <summary>Dollar amount allocated to Housing.</summary>
    public decimal Housing { get; set; }
    /// <summary>Dollar amount allocated to Food.</summary>
    public decimal Food { get; set; }
    /// <summary>Dollar amount allocated to Transportation.</summary>
    public decimal Transportation { get; set; }
    /// <summary>Dollar amount allocated to Utilities.</summary>
    public decimal Utilities { get; set; }
    /// <summary>Dollar amount allocated to Insurance.</summary>
    public decimal Insurance { get; set; }
    /// <summary>Dollar amount allocated to Healthcare.</summary>
    public decimal Healthcare { get; set; }
    /// <summary>Dollar amount allocated to Savings.</summary>
    public decimal Savings { get; set; }
    /// <summary>Dollar amount allocated to Debt Repayment.</summary>
    public decimal DebtRepayment { get; set; }
    /// <summary>Dollar amount allocated to Entertainment.</summary>
    public decimal Entertainment { get; set; }
    /// <summary>Dollar amount allocated to Miscellaneous.</summary>
    public decimal Miscellaneous { get; set; }
}

public class SaveBudgetResponseDto
{
    public bool Success { get; set; }
}

// =========================================================================
// Spreadsheet Analysis DTOs
// =========================================================================

public class AnalysisDashboardDto
{
    public bool HasData { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public BudgetDataSource ActiveSource { get; set; } = BudgetDataSource.GoogleSheet;
    public SummaryStatisticsDto? Summary { get; set; }
    public SpendingVelocityDto? Velocity { get; set; }
    public TrendDataDto? Trends { get; set; }
    public CategoryInsightsDto? Categories { get; set; }
    public BudgetVarianceDto? Variance { get; set; }
}

public class SummaryStatisticsDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }
    public double SavingsRate { get; set; }
    public int SnapshotCount { get; set; }
}

public class SpendingVelocityDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal DailyBurnRate { get; set; }
    public decimal ProjectedMonthlySpend { get; set; }
    public decimal BudgetedExpenses { get; set; }
    public double DaysElapsed { get; set; }
    public double DaysRemaining { get; set; }
    public double BudgetConsumptionPercent { get; set; }
    public double TimeElapsedPercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<WeeklySpendingDto> WeeklyTrend { get; set; } = new();
    public List<CategoryVelocityDto> CategoryVelocities { get; set; } = new();
}

public class WeeklySpendingDto
{
    public string WeekLabel { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int WeekNumber { get; set; }
}

public class CategoryVelocityDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public double ConsumptionPercent { get; set; }
    public double TimeElapsedPercent { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TrendDataDto
{
    public int Year { get; set; }
    public List<MonthlyTrendPointDto> TrendPoints { get; set; } = new();
}

public class MonthlyTrendPointDto
{
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetSavings { get; set; }
    public double SavingsRate { get; set; }
}

public class CategoryInsightsDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<CategoryBreakdownDto> Categories { get; set; } = new();
    public decimal TotalSpent { get; set; }
    public decimal TotalBudgeted { get; set; }
}

public class CategoryBreakdownDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double PercentageUsed { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BudgetVarianceDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<VarianceRankingDto> Rankings { get; set; } = new();
}

public class VarianceRankingDto
{
    public int Rank { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double VariancePercent { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SyncSpreadsheetRequestDto
{
    public int ConnectionId { get; set; }
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool? Overwrite { get; set; }
}

public class SyncSpreadsheetResponseDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasConflict { get; set; }
    public List<MonthConflictDto> Conflicts { get; set; } = new();
    public int SnapshotsCreated { get; set; }
    public int TransactionsCreated { get; set; }
}

public class MonthConflictDto
{
    public int SnapshotId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}