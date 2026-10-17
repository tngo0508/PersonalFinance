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
    public GoalsOverviewDto GoalsOverview { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();

    /// <summary>True when the user has a non-deleted Google Drive connection (valid or awaiting re-authentication).</summary>
    public bool HasGoogleDriveConnection { get; set; }
}

public class NetWorthSummaryDto
{
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

public class RecentActivityDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
}