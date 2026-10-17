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
    public List<RecentActivityDto> RecentActivity { get; set; } = new();

    /// <summary>True when the user has a non-deleted Google Drive connection (valid or awaiting re-authentication).</summary>
    public bool HasGoogleDriveConnection { get; set; }
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