using PersonalFinance.ApiService.SpreadsheetAnalysis.Services;

namespace PersonalFinance.ApiService.SpreadsheetAnalysis;

/// <summary>
///     Result containing all dashboard data.
/// </summary>
public sealed class AnalysisDashboardResult
{
    public bool HasData { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public SummaryStatisticsDto? Summary { get; set; }
    public SpendingVelocityDto? Velocity { get; set; }
    public TrendDataDto? Trends { get; set; }
    public CategoryInsightsDto? Categories { get; set; }
    public BudgetVarianceDto? Variance { get; set; }
}

/// <summary>
///     Result of a sync operation.
/// </summary>
public sealed class SyncSpreadsheetBudgetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasConflict { get; set; }
    public List<MonthConflictInfo> Conflicts { get; set; } = new();
    public int SnapshotsCreated { get; set; }
    public int TransactionsCreated { get; set; }
}

/// <summary>
///     Information about a conflicting month during sync.
/// </summary>
public sealed class MonthConflictInfo
{
    public int SnapshotId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}