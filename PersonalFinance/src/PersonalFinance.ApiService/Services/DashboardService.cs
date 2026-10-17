using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for aggregating dashboard data from all features.
/// </summary>
public class DashboardService : IDashboardService
{
    /// <summary>FI Target = annual spending × 25 (the 4% rule, see GLOSSARY.md).</summary>
    private const decimal FiTargetMultiplier = 25m;

    private readonly AppDbContext _dbContext;

    public DashboardService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        // One private method per Dashboard section so each section can be extended independently.
        // Sequential queries on purpose: the request-scoped DbContext is not thread-safe (see spec).
        var netWorth = await GetNetWorthSummaryAsync(userId, cancellationToken);
        var budget = await GetBudgetSummaryAsync(userId, cancellationToken);
        var savingsGoals = await GetSavingsGoalsSummaryAsync(userId, cancellationToken);
        var recentActivity = await GetRecentActivityAsync(userId, cancellationToken);
        var hasGoogleDriveConnection = await HasGoogleDriveConnectionAsync(userId, cancellationToken);

        return new DashboardSummaryDto
        {
            NetWorth = netWorth,
            Budget = budget,
            SavingsGoals = savingsGoals,
            RecentActivity = recentActivity,
            HasGoogleDriveConnection = hasGoogleDriveConnection
        };
    }

    private async Task<NetWorthSummaryDto?> GetNetWorthSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        var parameters = await _dbContext.NetWorthParameters
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (parameters is null)
        {
            return null;
        }

        var netWorth = parameters.StartingBalance;
        var fiTarget = parameters.AnnualSpending * FiTargetMultiplier;
        decimal? progressToFi = fiTarget != 0m ? netWorth / fiTarget * 100m : null;
        decimal? savingsRate = parameters.AnnualIncome != 0m
            ? parameters.AnnualSavings / parameters.AnnualIncome * 100m
            : null;
        var projectionPoints = BuildProjectionPoints(parameters);
        var fiReachedAge = fiTarget > 0m
            ? projectionPoints.FirstOrDefault(p => p.Base >= fiTarget)?.Age
            : null;

        return new NetWorthSummaryDto
        {
            CurrentNetWorth = netWorth,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            BaseRoi = (double)parameters.BaseRoi,
            FiYears = fiReachedAge - parameters.CurrentAge,
            FiTarget = fiTarget,
            ProgressToFiPercent = progressToFi,
            FiTargetReached = progressToFi >= 100m,
            SavingsRatePercent = savingsRate,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge,
            FiReachedAge = fiReachedAge,
            ProjectionPoints = projectionPoints
        };
    }

    /// <summary>
    /// Projection points from the same calculation the Net Worth page uses, so both pages always agree.
    /// Each point is the end-of-year balance for that age. Empty when retirement age is at or below current age.
    /// </summary>
    private static List<ProjectionPointDto> BuildProjectionPoints(NetWorthParameters parameters)
    {
        if (parameters.RetirementAge <= parameters.CurrentAge)
        {
            return new List<ProjectionPointDto>();
        }

        var projection = NetWorthProjectionService.ComputeProjection(new NetWorthProjectionRequestDto
        {
            StartingBalance = parameters.StartingBalance,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge,
            BaseRoi = parameters.BaseRoi,
            ConservativeRoi = parameters.ConservativeRoi,
            AggressiveRoi = parameters.AggressiveRoi,
            AggressiveSavings = parameters.AggressiveSavings
        });

        // All three scenarios share the same years and ages; index-align them.
        return projection.Base.Years
            .Select((year, i) => new ProjectionPointDto
            {
                Year = year.Year,
                Age = year.Age,
                Base = year.EndingBalance,
                Conservative = projection.Conservative.Years[i].EndingBalance,
                Aggressive = projection.Aggressive.Years[i].EndingBalance
            })
            .ToList();
    }

    private async Task<BudgetSummaryDto?> GetBudgetSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        var budget = await _dbContext.UserBudgetAllocations
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Period)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (budget is null)
        {
            return null;
        }

        return new BudgetSummaryDto
        {
            MonthlyIncome = budget.MonthlyIncome,
            HousingPercent = budget.HousingPercent,
            FoodPercent = budget.FoodPercent,
            TransportationPercent = budget.TransportationPercent,
            UtilitiesPercent = budget.UtilitiesPercent,
            InsurancePercent = budget.InsurancePercent,
            HealthcarePercent = budget.HealthcarePercent,
            SavingsPercent = budget.SavingsPercent,
            DebtRepaymentPercent = budget.DebtRepaymentPercent,
            EntertainmentPercent = budget.EntertainmentPercent,
            MiscellaneousPercent = budget.MiscellaneousPercent,
            Period = budget.Period
        };
    }

    private async Task<List<SavingsGoalSummaryDto>> GetSavingsGoalsSummaryAsync(string userId,
        CancellationToken cancellationToken)
    {
        var goals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Take(5)
            .Select(g => new SavingsGoalSummaryDto
            {
                Id = g.Id,
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount,
                ProgressPercent = g.TargetAmount > 0 ? (double)(g.CurrentAmount / g.TargetAmount) * 100 : 0,
                IsCompleted = g.IsCompleted
            })
            .ToListAsync(cancellationToken);

        return goals;
    }

    private async Task<List<RecentActivityDto>> GetRecentActivityAsync(string userId,
        CancellationToken cancellationToken)
    {
        var activities = new List<RecentActivityDto>();

        // Get recent transactions
        var recentTransactions = await _dbContext.SpreadsheetTransactionRecords
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(5)
            .Select(t => new RecentActivityDto
            {
                Title = t.Description,
                Description = $"{t.Category} - {t.Amount:C}",
                Date = t.Date,
                Type = t.Type
            })
            .ToListAsync(cancellationToken);

        activities.AddRange(recentTransactions);

        // Get recent savings goals
        var recentGoals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Take(3)
            .Select(g => new RecentActivityDto
            {
                Title = g.Name,
                Description = $"Target: {g.TargetAmount:C} | Saved: {g.CurrentAmount:C}",
                Date = g.CreatedAt,
                Type = "SavingsGoal"
            })
            .ToListAsync(cancellationToken);

        activities.AddRange(recentGoals);

        // Sort by date descending and take top 10 (OrderBy is stable, so ties keep query order)
        return activities.OrderByDescending(a => a.Date).Take(10).ToList();
    }

    /// <summary>
    /// A connection counts as long as it exists and is not soft-deleted, even when it needs re-authentication
    /// (<c>IsValid == false</c>): the user should repair it from Google Drive, not be asked to connect again.
    /// </summary>
    private Task<bool> HasGoogleDriveConnectionAsync(string userId, CancellationToken cancellationToken)
    {
        return _dbContext.GoogleDriveConnections
            .AsNoTracking()
            .AnyAsync(c => c.UserId == userId, cancellationToken);
    }
}
