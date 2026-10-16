namespace PersonalFinance.ApiService.SpreadsheetAnalysis.Services;

/// <summary>
///     Result DTO for summary statistics of a specific month.
/// </summary>
public sealed class SummaryStatisticsDto
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

/// <summary>
///     Result DTO for spending velocity analysis of a specific month.
/// </summary>
public sealed class SpendingVelocityDto
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

/// <summary>
///     Weekly spending data point for trend analysis.
/// </summary>
public sealed class WeeklySpendingDto
{
    public string WeekLabel { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int WeekNumber { get; set; }
}

/// <summary>
///     Per-category budget consumption rate.
/// </summary>
public sealed class CategoryVelocityDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public double ConsumptionPercent { get; set; }
    public double TimeElapsedPercent { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
///     Result DTO for trend data across months in a year.
/// </summary>
public sealed class TrendDataDto
{
    public int Year { get; set; }
    public List<MonthlyTrendPointDto> TrendPoints { get; set; } = new();
}

/// <summary>
///     Single month data point for trend charts.
/// </summary>
public sealed class MonthlyTrendPointDto
{
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetSavings { get; set; }
    public double SavingsRate { get; set; }
}

/// <summary>
///     Result DTO for category insights of a specific month.
/// </summary>
public sealed class CategoryInsightsDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<CategoryBreakdownDto> Categories { get; set; } = new();
    public decimal TotalSpent { get; set; }
    public decimal TotalBudgeted { get; set; }
}

/// <summary>
///     Per-category breakdown with budget vs actual comparison.
/// </summary>
public sealed class CategoryBreakdownDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double PercentageUsed { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
///     Result DTO for budget variance analysis ranked by category.
/// </summary>
public sealed class BudgetVarianceDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<VarianceRankingDto> Rankings { get; set; } = new();
}

/// <summary>
///     Single category variance ranking entry.
/// </summary>
public sealed class VarianceRankingDto
{
    public int Rank { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double VariancePercent { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
///     Service interface for analyzing spreadsheet budget data stored in the database.
/// </summary>
public interface ISpreadsheetAnalysisService
{
    /// <summary>
    ///     Gets summary statistics for a specific month, aggregating data from all snapshots.
    /// </summary>
    Task<SummaryStatisticsDto?> GetSummaryStatisticsAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets spending velocity analysis for a specific month including daily burn rate,
    ///     weekly trend, and per-category budget consumption rate.
    /// </summary>
    Task<SpendingVelocityDto?> GetSpendingVelocityAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets month-over-month trend data for a year, showing income, expenses, and savings rate.
    /// </summary>
    Task<TrendDataDto> GetTrendDataAsync(string userId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets category-level spending insights for a specific month with over/under budget highlights.
    /// </summary>
    Task<CategoryInsightsDto?> GetCategoryInsightsAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets budget vs actual variance ranked by category for a specific month.
    /// </summary>
    Task<BudgetVarianceDto?> GetBudgetVarianceAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all available years that have synced data for the user.
    /// </summary>
    Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all available months for a given year that have synced data for the user.
    /// </summary>
    Task<List<int>> GetAvailableMonthsAsync(string userId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the most recent synced month for the user, or null if no data exists.
    /// </summary>
    Task<(int year, int month)?> GetMostRecentMonthAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Syncs budget data from a Google Drive spreadsheet to the database.
    /// </summary>
    Task<SyncSpreadsheetBudgetResult> SyncSpreadsheetBudgetAsync(
        string userId,
        int connectionId,
        string fileId,
        string fileName,
        bool overwrite,
        CancellationToken cancellationToken = default);
}