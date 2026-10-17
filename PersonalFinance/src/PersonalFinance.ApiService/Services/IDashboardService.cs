namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Summary of net worth data for the Dashboard.
/// </summary>
public class NetWorthSummaryDto
{
    public decimal CurrentNetWorth { get; set; }
    public decimal AnnualSavings { get; set; }
    public decimal AnnualIncome { get; set; }
    public decimal AnnualSpending { get; set; }
    public double BaseRoi { get; set; }
    public int? FiYears { get; set; }
}

/// <summary>
/// Summary of budget data for the Dashboard.
/// </summary>
public class BudgetSummaryDto
{
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
}

/// <summary>
/// Summary of a savings goal for the Dashboard.
/// </summary>
public class SavingsGoalSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public double ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Savings goals totals aggregated across all of the user's goals (not just the five listed).
/// </summary>
public class GoalsOverviewDto
{
    public decimal TotalSaved { get; set; }
    public decimal TotalTarget { get; set; }

    /// <summary>TotalSaved / TotalTarget × 100, capped at 100; null when TotalTarget is zero.</summary>
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
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Full dashboard summary aggregating all features.
/// </summary>
public class DashboardSummaryDto
{
    public NetWorthSummaryDto? NetWorth { get; set; }
    public BudgetSummaryDto? Budget { get; set; }
    public List<SavingsGoalSummaryDto> SavingsGoals { get; set; } = new();
    public GoalsOverviewDto GoalsOverview { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
}

/// <summary>
/// Service for aggregating dashboard data from all features.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets a summary of all dashboard data for a user.
    /// </summary>
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default);
}