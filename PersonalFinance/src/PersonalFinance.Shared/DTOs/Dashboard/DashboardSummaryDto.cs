using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.Dashboard;

/// <summary>
/// Summary of net worth data for the Dashboard. Every amount is in <see cref="CurrencyCode"/>: the Reporting Currency,
/// the saved parameters converted once at the latest rate (ADR 0016), or the parameters' own currency when no rate
/// exists.
/// </summary>
public class NetWorthSummaryDto
{
    /// <summary>The ISO 4217 currency of every amount here.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal CurrentNetWorth { get; set; }
    public decimal AnnualSavings { get; set; }
    public decimal AnnualIncome { get; set; }
    public decimal AnnualSpending { get; set; }
    public double BaseRoi { get; set; }
    public int? FiYears { get; set; }

    /// <summary>FI Target: annual spending × 25 (the 4% rule).</summary>
    public decimal FiTarget { get; set; }

    /// <summary>Progress to FI: net worth as a percentage of the FI Target (uncapped); null when the FI Target is 0.</summary>
    public decimal? ProgressToFiPercent { get; set; }

    /// <summary>True when Progress to FI is at least 100%.</summary>
    public bool FiTargetReached { get; set; }

    /// <summary>Savings Rate: annual savings as a percentage of annual income; null when income is 0.</summary>
    public decimal? SavingsRatePercent { get; set; }

    /// <summary>The user's current age from Net Worth settings.</summary>
    public int CurrentAge { get; set; }

    /// <summary>The user's target retirement age from Net Worth settings.</summary>
    public int RetirementAge { get; set; }

    /// <summary>Age at which the Base scenario first reaches the FI Target; null when it never does or the FI Target is 0.</summary>
    public int? FiReachedAge { get; set; }

    /// <summary>
    /// Year-by-year projection from current age through retirement age, from the Net Worth projection calculation;
    /// empty when retirement age is at or below current age.
    /// </summary>
    public List<ProjectionPointDto> ProjectionPoints { get; set; } = new();
}

/// <summary>
/// One year of the Dashboard net worth projection: end-of-year balance for each scenario.
/// </summary>
public class ProjectionPointDto
{
    public int Year { get; set; }
    public int Age { get; set; }
    public decimal Base { get; set; }
    public decimal Conservative { get; set; }
    public decimal Aggressive { get; set; }
}

/// <summary>
/// Summary of budget data for the Dashboard.
/// </summary>
public class BudgetSummaryDto
{
    /// <summary>The ISO 4217 currency of every amount here: the Reporting Currency, or the budget's own when no rate exists.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal MonthlyIncome { get; set; }
    public decimal? HousingPercent { get; set; }
    public decimal? FoodPercent { get; set; }
    public decimal? TransportationPercent { get; set; }
    public decimal? UtilitiesPercent { get; set; }
    public decimal? InsurancePercent { get; set; }
    public decimal? HealthcarePercent { get; set; }
    public decimal? SavingsPercent { get; set; }
    public decimal? DebtRepaymentPercent { get; set; }
    public decimal? EntertainmentPercent { get; set; }
    public decimal? MiscellaneousPercent { get; set; }
    public string? Period { get; set; }

    /// <summary>All ten budget categories in Budget-page order, each with its percent and dollar amount.</summary>
    public List<BudgetAllocationDto> Allocations { get; set; } = new();

    /// <summary>Sum of all category percentages.</summary>
    public decimal TotalAllocatedPercent { get; set; }

    /// <summary>Percent of monthly income not allocated; null when allocations total 100% or more.</summary>
    public decimal? UnallocatedPercent { get; set; }

    /// <summary>Dollar amount of monthly income not allocated; null when allocations total 100% or more.</summary>
    public decimal? UnallocatedAmount { get; set; }
}

/// <summary>
/// One budget category's planned share of monthly income.
/// </summary>
public class BudgetAllocationDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable, CSS-friendly key for the category ("Debt Repayment" becomes "debt-repayment"), used by the view and
    /// the donut to pick the category's colour token so a category keeps its colour whichever categories are drawn.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public decimal Percent { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Summary of a savings goal for the Dashboard.
/// </summary>
public class SavingsGoalSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The ISO 4217 currency of the goal's amounts.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }

    /// <summary><see cref="TargetAmount"/> in the Reporting Currency at the latest rate; null when no rate exists.</summary>
    public decimal? ReportingTargetAmount { get; set; }

    /// <summary><see cref="CurrentAmount"/> in the Reporting Currency at the latest rate; null when no rate exists.</summary>
    public decimal? ReportingCurrentAmount { get; set; }

    public double ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Savings goals totals aggregated across all of the user's goals (not just the five listed), in the Reporting
/// Currency: each goal converted at the latest rate, the sum rounded once (ADR 0016).
/// </summary>
public class GoalsOverviewDto
{
    public decimal TotalSaved { get; set; }
    public decimal TotalTarget { get; set; }

    /// <summary>
    /// Sum of each goal's saved amount capped at its target, divided by TotalTarget × 100 (so an over-funded goal
    /// cannot mask unfunded ones); null when TotalTarget is zero.
    /// </summary>
    public decimal? FundedPercent { get; set; }

    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>
/// Recent activity item for the Dashboard.
/// </summary>
public class RecentActivityDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>English description, kept for clients that do not compose their own from the fields below.</summary>
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;

    /// <summary>A transaction's category as stored (user data, never translated); null for a savings goal.</summary>
    public string? Category { get; set; }

    /// <summary>A transaction's amount, in <see cref="CurrencyCode"/>; null for a savings goal.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The ISO 4217 currency of the item's amounts (the transaction's, or the goal's).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>A savings goal's target amount; null for a transaction.</summary>
    public decimal? TargetAmount { get; set; }

    /// <summary>A savings goal's saved amount; null for a transaction.</summary>
    public decimal? SavedAmount { get; set; }
}

/// <summary>
/// Provider-backed monthly budget figures (Google Sheet snapshot or Budget Tracker monthly plan) for the most recent month.
/// </summary>
public class DashboardMonthlyBudgetDto
{
    /// <summary>The ISO 4217 currency of the figures: the Reporting Currency (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The rates that brought the month's lines and transactions into the Reporting Currency.</summary>
    public ConversionDto? Conversion { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal BudgetedIncome { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal BudgetedExpenses { get; set; }
    public decimal ActualExpenses { get; set; }
}

/// <summary>
/// Full dashboard summary aggregating all features.
/// </summary>
public class DashboardSummaryDto
{
    public NetWorthSummaryDto? NetWorth { get; set; }
    public BudgetSummaryDto? Budget { get; set; }
    public DashboardMonthlyBudgetDto? MonthlyBudget { get; set; }
    public List<SavingsGoalSummaryDto> SavingsGoals { get; set; } = new();
    public GoalsOverviewDto GoalsOverview { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();

    /// <summary>The user's active budget data source.</summary>
    public BudgetDataSource ActiveSource { get; set; } = BudgetDataSource.GoogleSheet;

    /// <summary>True when the user has a non-deleted Google Drive connection (valid or awaiting re-authentication).</summary>
    public bool HasGoogleDriveConnection { get; set; }

    /// <summary>The Account's Reporting Currency, which every total on the Dashboard is in (ADR 0016).</summary>
    public string ReportingCurrency { get; set; } = string.Empty;

    /// <summary>The latest rates that brought the Dashboard's "now" figures into the Reporting Currency.</summary>
    public ConversionDto? Conversion { get; set; }
}
