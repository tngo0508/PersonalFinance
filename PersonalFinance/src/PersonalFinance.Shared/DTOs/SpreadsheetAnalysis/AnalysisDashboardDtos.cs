using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

public class AnalysisDashboardDto
{
    public bool HasData { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public BudgetDataSource ActiveSource { get; set; } = BudgetDataSource.GoogleSheet;

    /// <summary>How much data each Data Source holds, Google Sheet first; Analysis offers removal for those with data.</summary>
    public List<BudgetDataSourceSummaryDto> SourceSummaries { get; set; } = new();

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
    public decimal SavingsRate { get; set; }
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
    public decimal SavingsRate { get; set; }
}

public class CategoryInsightsDto
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<CategoryBreakdownDto> Categories { get; set; } = new();
    public decimal TotalSpent { get; set; }
    public decimal TotalBudgeted { get; set; }

    /// <summary>The month's actual income, even when no month was requested (the summary then covers the year).</summary>
    public decimal ActualIncome { get; set; }

    /// <summary>The month's actual expenses, from the same month as <see cref="Categories"/>.</summary>
    public decimal ActualExpenses { get; set; }
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
