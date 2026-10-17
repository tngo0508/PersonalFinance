using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service for aggregating dashboard data from all features.
/// </summary>
public class DashboardService : IDashboardService
{
    /// <summary>Number of savings goals listed on the Dashboard.</summary>
    private const int ListedGoalCount = 5;

    /// <summary>Number of the newest savings goals considered for recent activity.</summary>
    private const int ActivityGoalCount = 3;

    /// <summary>Number of recent activity items shown on the Dashboard.</summary>
    private const int ActivityItemCount = 5;

    private readonly IBudgetDataProvider _budgetDataProvider;

    private readonly AppDbContext _dbContext;

    public DashboardService(AppDbContext dbContext, IBudgetDataProvider budgetDataProvider)
    {
        _dbContext = dbContext;
        _budgetDataProvider = budgetDataProvider;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        // One private method per Dashboard section so each section can be extended independently.
        // Sequential queries on purpose: the request-scoped DbContext is not thread-safe (see spec).
        var netWorth = await GetNetWorthSummaryAsync(userId, cancellationToken);
        var budget = await GetBudgetSummaryAsync(userId, cancellationToken);
        var monthlyBudget = await GetMonthlyBudgetSummaryAsync(userId, cancellationToken);
        var newestGoals = await GetNewestGoalsAsync(userId, cancellationToken);
        var goalsOverview = await GetGoalsOverviewAsync(userId, cancellationToken);
        var recentActivity = await GetRecentActivityAsync(userId, newestGoals, cancellationToken);
        var hasGoogleDriveConnection = await HasGoogleDriveConnectionAsync(userId, cancellationToken);
        var activeSource = await _budgetDataProvider.GetActiveSourceAsync(userId, cancellationToken);

        return new DashboardSummaryDto
        {
            NetWorth = netWorth,
            Budget = budget,
            MonthlyBudget = monthlyBudget,
            SavingsGoals = newestGoals.Select(g => g.Summary).ToList(),
            GoalsOverview = goalsOverview,
            RecentActivity = recentActivity,
            ActiveSource = activeSource,
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

        // FI Target and projection both come from the Net Worth page's calculation, so the pages always agree.
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

        var netWorth = parameters.StartingBalance;
        var fiTarget = projection.FiTarget;
        decimal? progressToFi = fiTarget != 0m ? netWorth / fiTarget * 100m : null;
        decimal? savingsRate = parameters.AnnualIncome != 0m
            ? parameters.AnnualSavings / parameters.AnnualIncome * 100m
            : null;
        var projectionPoints = BuildProjectionPoints(parameters, projection);
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
    private static List<ProjectionPointDto> BuildProjectionPoints(NetWorthParameters parameters,
        NetWorthProjectionDto projection)
    {
        if (parameters.RetirementAge <= parameters.CurrentAge)
        {
            return new List<ProjectionPointDto>();
        }

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

    private async Task<DashboardMonthlyBudgetDto?> GetMonthlyBudgetSummaryAsync(string userId,
        CancellationToken cancellationToken)
    {
        var years = await _budgetDataProvider.GetAvailableYearsAsync(userId, cancellationToken);
        if (!years.Any())
        {
            return null;
        }

        var latestYear = years.Max();
        var months = await _budgetDataProvider.GetMonthsAsync(userId, latestYear, cancellationToken);
        if (!months.Any())
        {
            return null;
        }

        var latestMonth = months.OrderByDescending(m => m.Month).First();
        var monthName =
            new DateTime(latestYear, latestMonth.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        return new DashboardMonthlyBudgetDto
        {
            Year = latestYear,
            Month = latestMonth.Month,
            Period = monthName,
            BudgetedIncome = latestMonth.BudgetedIncome,
            ActualIncome = latestMonth.ActualIncome,
            BudgetedExpenses = latestMonth.BudgetedExpenses,
            ActualExpenses = latestMonth.ActualExpenses
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
                Key = CategoryKey(category.Item1),
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

    /// <summary>CSS-friendly key for a category name ("Debt Repayment" becomes "debt-repayment").</summary>
    private static string CategoryKey(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');

    /// <summary>Dollar share of monthly income for a percentage. Unrounded; rounding happens at presentation.</summary>
    private static decimal ToMonthlyAmount(decimal monthlyIncome, decimal percent) => percent / 100m * monthlyIncome;

    /// <summary>
    /// The user's newest goals, newest first. One query serves both the goals list and the goal entries in recent
    /// activity (which are always among the newest goals).
    /// </summary>
    private async Task<List<NewestGoal>> GetNewestGoalsAsync(string userId, CancellationToken cancellationToken)
    {
        var goals = await _dbContext.SavingsGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Take(ListedGoalCount)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.TargetAmount,
                g.CurrentAmount,
                g.IsCompleted,
                g.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return goals
            .Select(g => new NewestGoal(
                new SavingsGoalSummaryDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    TargetAmount = g.TargetAmount,
                    CurrentAmount = g.CurrentAmount,
                    ProgressPercent = g.TargetAmount > 0 ? (double)(g.CurrentAmount / g.TargetAmount) * 100 : 0,
                    IsCompleted = g.IsCompleted
                },
                g.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Totals across all of the user's (non-deleted) goals, aggregated database-side in a single query.
    /// Funded percent caps each goal's saved amount at its target, so an over-funded goal cannot mask unfunded ones.
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
                FundedSaved = grp.Sum(g => g.CurrentAmount > g.TargetAmount ? g.TargetAmount : g.CurrentAmount),
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
                ? Math.Min(totals.FundedSaved / totals.TotalTarget * 100m, 100m)
                : null,
            CompletedCount = totals.CompletedCount,
            TotalCount = totals.TotalCount
        };
    }

    /// <summary>
    /// The recent activity items exactly as displayed: newest first, at most <see cref="ActivityItemCount"/>.
    /// Ties on date put transactions before goals, then the newest record (highest Id) first.
    /// </summary>
    private async Task<List<RecentActivityDto>> GetRecentActivityAsync(string userId, List<NewestGoal> newestGoals,
        CancellationToken cancellationToken)
    {
        var recentTransactions = await _budgetDataProvider.GetTransactionsAsync(
            userId,
            DateOnly.MinValue,
            DateOnly.MaxValue,
            ActivityItemCount,
            cancellationToken);

        var transactionItems = recentTransactions.Select(t => new
        {
            Source = 0,
            t.Id,
            Item = new RecentActivityDto
            {
                Title = t.Description,
                Description = $"{t.Category} - {t.Amount:C}",
                Date = t.Date,
                Type = t.Type
            }
        });

        // The newest goals are already loaded for the goals list; no extra query.
        var goalItems = newestGoals.Take(ActivityGoalCount).Select(g => new
        {
            Source = 1,
            g.Summary.Id,
            Item = new RecentActivityDto
            {
                Title = g.Summary.Name,
                Description = $"Target: {g.Summary.TargetAmount:C} | Saved: {g.Summary.CurrentAmount:C}",
                Date = g.CreatedAt,
                Type = "SavingsGoal"
            }
        });

        return transactionItems
            .Concat(goalItems)
            .OrderByDescending(a => a.Item.Date)
            .ThenBy(a => a.Source)
            .ThenByDescending(a => a.Id)
            .Take(ActivityItemCount)
            .Select(a => a.Item)
            .ToList();
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

    /// <summary>A listed savings goal together with its creation time, which recent activity needs.</summary>
    private sealed record NewestGoal(SavingsGoalSummaryDto Summary, DateTime CreatedAt);
}