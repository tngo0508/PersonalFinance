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
    /// <summary>The ISO 4217 code every amount on the page is in: the Account's Reporting Currency (ADR 0016).</summary>
    public string ReportingCurrency { get; set; } = "USD";

    /// <summary>The rates that brought the period's months into the Reporting Currency; null when none was needed.</summary>
    public PersonalFinance.Shared.DTOs.Currency.ConversionDto? Conversion { get; set; }

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

    /// <summary>The month the velocity figures describe: the selected month, or the latest one when none is selected.</summary>
    public int VelocityMonth { get; set; }

    /// <summary>The number of days in <see cref="VelocityMonth"/>; 0 without velocity figures.</summary>
    public int DaysInVelocityMonth { get; set; }

    /// <summary>True while <see cref="VelocityMonth"/> still has days to come, so the page projects its end.</summary>
    public bool IsVelocityMonthOpen => DaysRemaining > 0;

    /// <summary>
    ///     How the month's spending pace reads at a glance: "over" past the budget, "ahead" spending faster than time
    ///     passes, otherwise "ok" (on track, or spending slower than time passes).
    /// </summary>
    public string PaceTone => VelocityStatus switch
    {
        "Over Budget" => "over",
        "Ahead of Schedule" => "ahead",
        _ => "ok"
    };

    /// <summary><see cref="VelocityStatus"/> in the request's Language.</summary>
    public string VelocityStatusLabel { get; set; } = string.Empty;

    // Trend data (for charts)
    public List<TrendPointViewModel> TrendPoints { get; set; } = new();

    // Category insights
    public List<CategoryInsightViewModel> Categories { get; set; } = new();

    // Money Flow: one month of income moving into categories and savings; null when the month has no data
    public MoneyFlowViewModel? MoneyFlow { get; set; }

    // Weekly trend
    public List<WeeklySpendingViewModel> WeeklyTrend { get; set; } = new();

    // Category velocities
    public List<CategoryVelocityViewModel> CategoryVelocities { get; set; } = new();

    /// <summary>The selected month's point in <see cref="TrendPoints"/>; null for a whole-year view.</summary>
    public TrendPointViewModel? SelectedTrendPoint =>
        SelectedMonth is { } month ? TrendPoints.FirstOrDefault(t => t.MonthNumber == month) : null;

    /// <summary>The month before the selected one, for the summary's month-on-month change; null when there is none.</summary>
    public TrendPointViewModel? PreviousTrendPoint =>
        SelectedMonth is { } month ? TrendPoints.FirstOrDefault(t => t.MonthNumber == month - 1) : null;

    /// <summary>
    ///     The categories most in need of attention first: the highest share of budget used, then by name. A category with
    ///     neither a budget nor spending says nothing about the period and is left out.
    /// </summary>
    public List<CategoryInsightViewModel> CategoriesByAttention =>
        Categories.Where(c => c.BudgetedAmount != 0m || c.ActualAmount != 0m)
            .OrderByDescending(c => c.PercentageUsed).ThenBy(c => c.CategoryName, StringComparer.CurrentCulture).ToList();

    /// <summary>The largest budget or actual amount among the categories: the full width of every category bar.</summary>
    public decimal CategoryScaleMax =>
        Categories.Count == 0 ? 0m : Categories.Max(c => Math.Max(c.BudgetedAmount, c.ActualAmount));

    // Status messages
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class TrendPointViewModel
{
    /// <summary>The month, 1–12, so the page can find the selected and previous months.</summary>
    public int MonthNumber { get; set; }

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

    /// <summary>How the category reads at a glance: "over" its budget, "near" its limit, otherwise "ok".</summary>
    public string Tone => Status switch
    {
        "Over Budget" => "over",
        "Near Limit" => "near",
        _ => "ok"
    };
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
