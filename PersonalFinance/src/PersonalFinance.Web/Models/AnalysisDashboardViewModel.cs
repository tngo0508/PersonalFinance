using System.Globalization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models;

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

    /// <summary>
    /// User-facing period label built from the selected year and month, e.g. "March 2026".
    /// Independent of whether the active source has a summary for that period.
    /// </summary>
    public string PeriodLabel
    {
        get
        {
            if (SelectedMonth is >= 1 and <= 12 && SelectedYear > 0)
            {
                return new DateTime(SelectedYear, SelectedMonth.Value, 1)
                    .ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            }

            if (SelectedYear > 0)
            {
                return SelectedYear.ToString(CultureInfo.InvariantCulture);
            }

            return "this period";
        }
    }

    // Summary statistics
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSavings { get; set; }
    public double SavingsRate { get; set; }
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

    // Trend data (for charts)
    public List<TrendPointViewModel> TrendPoints { get; set; } = new();

    // Category insights
    public List<CategoryInsightViewModel> Categories { get; set; } = new();

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
    public double SavingsRate { get; set; }
}

public sealed class CategoryInsightViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public double PercentageUsed { get; set; }
    public string Status { get; set; } = string.Empty;
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