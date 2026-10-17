using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;

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
        var goalsOverview = await GetGoalsOverviewAsync(userId, cancellationToken);
        var recentActivity = await GetRecentActivityAsync(userId, cancellationToken);
        var hasGoogleDriveConnection = await HasGoogleDriveConnectionAsync(userId, cancellationToken);

        return new DashboardSummaryDto
        {
            NetWorth = netWorth,
            Budget = budget,
            SavingsGoals = savingsGoals,
            GoalsOverview = goalsOverview,
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

        return new NetWorthSummaryDto
        {
            CurrentNetWorth = netWorth,
            AnnualSavings = parameters.AnnualSavings,
            AnnualIncome = parameters.AnnualIncome,
            AnnualSpending = parameters.AnnualSpending,
            BaseRoi = (double)parameters.BaseRoi,
            FiYears = null,
            FiTarget = fiTarget,
            ProgressToFiPercent = progressToFi,
            FiTargetReached = progressToFi >= 100m,
            SavingsRatePercent = savingsRate,
            CurrentAge = parameters.CurrentAge,
            RetirementAge = parameters.RetirementAge
        };
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

        // All ten categories in Budget-page order. Zero categories stay in the list so the totals are honest;
        // the view decides what to draw.
        var allocations = new[]
            {
                ("Housing", budget.HousingPercent),
                ("Food", budget.FoodPercent),
                ("Transportation", budget.TransportationPercent),
                ("Utilities", budget.UtilitiesPercent),
                ("Insurance", budget.InsurancePercent),
                ("Healthcare", budget.HealthcarePercent),
                ("Savings", budget.SavingsPercent),
                ("Debt Repayment", budget.DebtRepaymentPercent),
                ("Entertainment", budget.EntertainmentPercent),
                ("Miscellaneous", budget.MiscellaneousPercent)
            }
            .Select(category => new BudgetAllocationDto
            {
                Name = category.Item1,
                Percent = category.Item2,
                Amount = ToMonthlyAmount(budget.MonthlyIncome, category.Item2)
            })
            .ToList();

        var totalAllocatedPercent = allocations.Sum(a => a.Percent);
        decimal? unallocatedPercent = totalAllocatedPercent < 100m ? 100m - totalAllocatedPercent : null;

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
            Period = budget.Period,
            Allocations = allocations,
            TotalAllocatedPercent = totalAllocatedPercent,
            UnallocatedPercent = unallocatedPercent,
            UnallocatedAmount = unallocatedPercent is { } remainder
                ? ToMonthlyAmount(budget.MonthlyIncome, remainder)
                : null
        };
    }

    /// <summary>Dollar share of monthly income for a percentage. Unrounded; rounding happens at presentation.</summary>
    private static decimal ToMonthlyAmount(decimal monthlyIncome, decimal percent) => percent / 100m * monthlyIncome;

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

    /// <summary>
    /// Totals across all of the user's (non-deleted) goals, aggregated database-side in a single query.
    /// </summary>
    private async Task<GoalsOverviewDto> GetGoalsOverviewAsync(string userId, CancellationToken cancellationToken)
    {
        var totals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .GroupBy(_ => 1)
            .Select(grp => new
            {
                TotalSaved = grp.Sum(g => g.CurrentAmount),
                TotalTarget = grp.Sum(g => g.TargetAmount),
                CompletedCount = grp.Count(g => g.IsCompleted),
                TotalCount = grp.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null)
        {
            return new GoalsOverviewDto();
        }

        return new GoalsOverviewDto
        {
            TotalSaved = totals.TotalSaved,
            TotalTarget = totals.TotalTarget,
            FundedPercent = totals.TotalTarget > 0
                ? Math.Min(totals.TotalSaved / totals.TotalTarget * 100m, 100m)
                : null,
            CompletedCount = totals.CompletedCount,
            TotalCount = totals.TotalCount
        };
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
