using System.Globalization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Analysis;

/// <summary>
///     View model for the Spreadsheet Analysis dashboard page.
/// </summary>
public class AnalysisDashboardViewModel
{
    public bool HasData { get; set; }
    public int SelectedYear { get; set; }
    public int? SelectedMonth { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public BudgetDataSource ActiveSource { get; set; } = BudgetDataSource.GoogleSheet;

    /// <summary>The Data Sources holding any data, which the page offers to remove (Data Source Removal).</summary>
    public List<BudgetDataSource> RemovableSources { get; set; } = new();

    /// <summary>
    /// User-facing period label built from the selected year and month in the request culture, e.g. "March 2026";
    /// null when no period is selected, so the view says "this period" in the request's Language.
    /// Independent of whether the active source has a summary for that period.
    /// </summary>
    public string? PeriodLabel
    {
        get
        {
            if (SelectedMonth is >= 1 and <= 12 && SelectedYear > 0)
            {
                return AnalysisText.MonthYear(SelectedYear, SelectedMonth.Value);
            }

            if (SelectedYear > 0)
            {
                return SelectedYear.ToString(CultureInfo.InvariantCulture);
            }

            return null;
        }
    }

    // Summary statistics
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }
    public decimal SavingsRate { get; set; }
    public string MonthName { get; set; } = string.Empty;

    // Spending velocity
    public decimal DailyBurnRate { get; set; }
    public decimal ProjectedMonthlySpend { get; set; }
    public decimal BudgetedExpenses { get; set; }
    public double DaysElapsed { get; set; }
    public double DaysRemaining { get; set; }
    public double BudgetConsumptionPercent { get; set; }
    public double TimeElapsedPercent { get; set; }
    public string VelocityStatus { get; set; } = string.Empty;

    /// <summary><see cref="VelocityStatus"/> in the request's Language.</summary>
    public string VelocityStatusLabel { get; set; } = string.Empty;

    // Trend data (for charts)
    public List<TrendPointViewModel> TrendPoints { get; set; } = new();

    // Category insights
    public List<CategoryInsightViewModel> Categories { get; set; } = new();

    // Money Flow: one month of income moving into categories and savings; null when the month has no data
    public MoneyFlowViewModel? MoneyFlow { get; set; }

    // Variance rankings
    public List<VarianceRankingViewModel> VarianceRankings { get; set; } = new();

    // Weekly trend
    public List<WeeklySpendingViewModel> WeeklyTrend { get; set; } = new();

    // Category velocities
    public List<CategoryVelocityViewModel> CategoryVelocities { get; set; } = new();

    // Status messages
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class TrendPointViewModel
{
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetSavings { get; set; }
    public decimal SavingsRate { get; set; }
}

public sealed class CategoryInsightViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double PercentageUsed { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary><see cref="Status"/> in the request's Language.</summary>
    public string StatusLabel { get; set; } = string.Empty;
}

public sealed class VarianceRankingViewModel
{
    public int Rank { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double VariancePercent { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary><see cref="Status"/> in the request's Language.</summary>
    public string StatusLabel { get; set; } = string.Empty;
}

public sealed class WeeklySpendingViewModel
{
    public string WeekLabel { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
}

public sealed class CategoryVelocityViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public double ConsumptionPercent { get; set; }
    public double TimeElapsedPercent { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
///     View model for the sync confirmation dialog.
/// </summary>
public class SyncConfirmationViewModel
{
    public int ConnectionId { get; set; }
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public List<MonthConflictDto> Conflicts { get; set; } = new();
}
