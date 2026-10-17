using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Dashboard operations.
/// </summary>
public interface IDashboardApi
{
    /// <summary>
    /// Gets a summary of all dashboard data for the current user.
    /// </summary>
    [Get("/api/Dashboard/summary")]
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

// =========================================================================
// DTOs
// =========================================================================

public class DashboardSummaryDto
{
    public NetWorthSummaryDto? NetWorth { get; set; }
    public BudgetSummaryDto? Budget { get; set; }
    public List<SavingsGoalSummaryDto> SavingsGoals { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
}

public class NetWorthSummaryDto
{
    public decimal CurrentNetWorth { get; set; }
    public decimal AnnualSavings { get; set; }
    public decimal AnnualIncome { get; set; }
    public decimal AnnualSpending { get; set; }
    public double BaseRoi { get; set; }
    public int? FiYears { get; set; }
}

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

public class SavingsGoalSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public double ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}

public class RecentActivityDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
}