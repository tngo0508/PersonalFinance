using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;

namespace PersonalFinance.ApiService.Services;

/// <summary>
///     Implementation of ISpreadsheetAnalysisService that queries the database
///     to provide analysis of synced spreadsheet budget data.
/// </summary>
public class SpreadsheetAnalysisService : ISpreadsheetAnalysisService
{
    private readonly AppDbContext _dbContext;

    public SpreadsheetAnalysisService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SummaryStatisticsDto?> GetSummaryStatisticsAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year);

        if (month.HasValue) query = query.Where(s => s.MonthNumber == month.Value);

        var snapshots = await query.ToListAsync(cancellationToken);

        if (!snapshots.Any()) return null;

        var totalIncome = snapshots.Sum(s => s.ActualIncome);
        var totalExpenses = snapshots.Sum(s => s.ActualExpenses);
        var netSavings = totalIncome - totalExpenses;
        var savingsRate = totalIncome > 0 ? (double)(netSavings / totalIncome) * 100 : 0;

        var monthNumber = month ?? snapshots.FirstOrDefault()?.MonthNumber ?? 0;
        var monthName = GetMonthName(monthNumber);

        return new SummaryStatisticsDto
        {
            Year = year,
            MonthNumber = monthNumber,
            MonthName = monthName,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetSavings = netSavings,
            SavingsRate = Math.Round(savingsRate, 1),
            SnapshotCount = snapshots.Count
        };
    }

    public async Task<SpendingVelocityDto?> GetSpendingVelocityAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = month ?? await GetCurrentMonthForVelocityAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == targetMonth)
            .ToListAsync(cancellationToken);

        if (!snapshots.Any()) return null;

        var now = DateTime.UtcNow;
        var isCurrentMonth = now.Year == year && now.Month == targetMonth;

        // Calculate days elapsed in the month
        double daysElapsed;
        double daysRemaining;
        int daysInMonth;

        if (isCurrentMonth)
        {
            daysInMonth = DateTime.DaysInMonth(year, targetMonth);
            daysElapsed = now.Day - 1 + now.Hour / 24.0;
            daysRemaining = daysInMonth - daysElapsed;
        }
        else
        {
            daysInMonth = DateTime.DaysInMonth(year, targetMonth);
            daysElapsed = daysInMonth;
            daysRemaining = 0;
        }

        var totalExpenses = snapshots.Sum(s => s.ActualExpenses);
        var budgetedExpenses = snapshots.Sum(s => s.BudgetedExpenses);

        var dailyBurnRate = daysElapsed > 0 ? totalExpenses / (decimal)daysElapsed : 0;
        var projectedMonthlySpend = daysInMonth > 0 ? dailyBurnRate * daysInMonth : 0;

        var budgetConsumptionPercent = budgetedExpenses > 0 ? (double)(totalExpenses / budgetedExpenses) * 100 : 0;
        var timeElapsedPercent = daysInMonth > 0 ? daysElapsed / daysInMonth * 100 : 0;

        string status;
        if (budgetConsumptionPercent > 100)
            status = "Over Budget";
        else if (budgetConsumptionPercent > timeElapsedPercent * 1.1)
            status = "Ahead of Schedule";
        else if (budgetConsumptionPercent < timeElapsedPercent * 0.9)
            status = "Behind Schedule";
        else
            status = "On Track";

        // Calculate weekly trend from transactions
        var weeklyTrend = await CalculateWeeklyTrendAsync(userId, year, targetMonth, cancellationToken);

        // Calculate category velocities
        var categoryVelocities =
            await CalculateCategoryVelocitiesAsync(userId, year, targetMonth, timeElapsedPercent, cancellationToken);

        return new SpendingVelocityDto
        {
            Year = year,
            MonthNumber = targetMonth,
            MonthName = GetMonthName(targetMonth),
            DailyBurnRate = Math.Round(dailyBurnRate, 2),
            ProjectedMonthlySpend = Math.Round(projectedMonthlySpend, 2),
            BudgetedExpenses = budgetedExpenses,
            DaysElapsed = Math.Round(daysElapsed, 1),
            DaysRemaining = Math.Round(daysRemaining, 1),
            BudgetConsumptionPercent = Math.Round(budgetConsumptionPercent, 1),
            TimeElapsedPercent = Math.Round(timeElapsedPercent, 1),
            Status = status,
            WeeklyTrend = weeklyTrend,
            CategoryVelocities = categoryVelocities
        };
    }

    public async Task<TrendDataDto> GetTrendDataAsync(string userId, int year,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .OrderBy(s => s.MonthNumber)
            .ToListAsync(cancellationToken);

        var trendPoints = snapshots.Select(s => new MonthlyTrendPointDto
        {
            MonthNumber = s.MonthNumber,
            MonthName = s.MonthName,
            Income = s.ActualIncome,
            Expenses = s.ActualExpenses,
            NetSavings = s.ActualIncome - s.ActualExpenses,
            SavingsRate = s.ActualIncome > 0
                ? Math.Round((double)((s.ActualIncome - s.ActualExpenses) / s.ActualIncome) * 100, 1)
                : 0
        }).ToList();

        return new TrendDataDto
        {
            Year = year,
            TrendPoints = trendPoints
        };
    }

    public async Task<CategoryInsightsDto?> GetCategoryInsightsAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = month ?? await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == targetMonth)
            .ToListAsync(cancellationToken);

        if (!snapshots.Any()) return null;

        var categories = new Dictionary<string, CategoryBreakdownDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            var breakdown = ParseCategoryBreakdown(snapshot.CategoryBreakdownJson);
            foreach (var category in breakdown)
            {
                if (!categories.TryGetValue(category.CategoryName, out var existing))
                {
                    existing = new CategoryBreakdownDto { CategoryName = category.CategoryName };
                    categories[category.CategoryName] = existing;
                }

                existing.BudgetedAmount += category.BudgetedAmount;
                existing.ActualAmount += category.ActualAmount;
            }
        }

        var categoryList = categories.Values.Select(c =>
        {
            c.Variance = c.BudgetedAmount - c.ActualAmount;
            c.PercentageUsed = c.BudgetedAmount > 0
                ? Math.Round((double)(c.ActualAmount / c.BudgetedAmount) * 100, 1)
                : 0;
            c.Status = GetCategoryStatus(c.PercentageUsed);
            return c;
        }).OrderByDescending(c => c.ActualAmount).ToList();

        var totalSpent = categoryList.Sum(c => c.ActualAmount);
        var totalBudgeted = categoryList.Sum(c => c.BudgetedAmount);

        return new CategoryInsightsDto
        {
            Year = year,
            MonthNumber = targetMonth,
            MonthName = GetMonthName(targetMonth),
            Categories = categoryList,
            TotalSpent = totalSpent,
            TotalBudgeted = totalBudgeted
        };
    }

    public async Task<BudgetVarianceDto?> GetBudgetVarianceAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = month ?? await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == targetMonth)
            .ToListAsync(cancellationToken);

        if (!snapshots.Any()) return null;

        var categories = new Dictionary<string, (decimal Budgeted, decimal Actual)>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            var breakdown = ParseCategoryBreakdown(snapshot.CategoryBreakdownJson);
            foreach (var category in breakdown)
            {
                if (!categories.TryGetValue(category.CategoryName, out var existing))
                {
                    existing = (0m, 0m);
                    categories[category.CategoryName] = existing;
                }

                existing = (existing.Budgeted + category.BudgetedAmount, existing.Actual + category.ActualAmount);
                categories[category.CategoryName] = existing;
            }
        }

        var rankings = categories.Select((kv, index) =>
        {
            var variance = kv.Value.Budgeted - kv.Value.Actual;
            var variancePercent = kv.Value.Budgeted > 0 ? (double)(variance / kv.Value.Budgeted) * 100 : 0;

            return new VarianceRankingDto
            {
                Rank = index + 1,
                CategoryName = kv.Key,
                BudgetedAmount = kv.Value.Budgeted,
                ActualAmount = kv.Value.Actual,
                Variance = variance,
                VariancePercent = Math.Round(variancePercent, 1),
                Status = variance >= 0 ? "Under Budget" : "Over Budget"
            };
        }).OrderByDescending(r => r.ActualAmount - r.BudgetedAmount).ToList();

        // Re-rank after sorting
        for (var i = 0; i < rankings.Count; i++) rankings[i].Rank = i + 1;

        return new BudgetVarianceDto
        {
            Year = year,
            MonthNumber = targetMonth,
            MonthName = GetMonthName(targetMonth),
            Rankings = rankings
        };
    }

    public async Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<int>> GetAvailableMonthsAsync(string userId, int year,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .Select(s => s.MonthNumber)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(cancellationToken);
    }

    public async Task<(int year, int month)?> GetMostRecentMonthAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.MonthNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return snapshot != null ? (snapshot.Year, snapshot.MonthNumber) : null;
    }

    #region Private Helper Methods

    private async Task<int> GetCurrentMonthForVelocityAsync(string userId, int year,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (now.Year == year) return now.Month;

        return await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
    }

    private async Task<int> GetMostRecentMonthNumberAsync(string userId, int year, CancellationToken cancellationToken)
    {
        var snapshot = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .OrderByDescending(s => s.MonthNumber)
            .Select(s => s.MonthNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return snapshot;
    }

    private async Task<List<WeeklySpendingDto>> CalculateWeeklyTrendAsync(string userId, int year, int month,
        CancellationToken cancellationToken)
    {
        var transactions = await _dbContext.SpreadsheetTransactionRecords
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date.Year == year && t.Date.Month == month)
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        if (!transactions.Any()) return new List<WeeklySpendingDto>();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var weeksInMonth = (int)Math.Ceiling(daysInMonth / 7.0);

        var weeklyData = new List<WeeklySpendingDto>();

        for (var week = 1; week <= weeksInMonth; week++)
        {
            var startDay = (week - 1) * 7 + 1;
            var endDay = Math.Min(week * 7, daysInMonth);

            var weekTransactions = transactions.Where(t => t.Date.Day >= startDay && t.Date.Day <= endDay);
            var totalSpent = weekTransactions.Sum(t => t.Amount);

            weeklyData.Add(new WeeklySpendingDto
            {
                WeekLabel = $"Week {week}",
                TotalSpent = Math.Round(totalSpent, 2),
                WeekNumber = week
            });
        }

        return weeklyData;
    }

    private async Task<List<CategoryVelocityDto>> CalculateCategoryVelocitiesAsync(string userId, int year, int month,
        double timeElapsedPercent, CancellationToken cancellationToken)
    {
        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == month)
            .ToListAsync(cancellationToken);

        if (!snapshots.Any()) return new List<CategoryVelocityDto>();

        var categories = new Dictionary<string, (decimal Budgeted, decimal Actual)>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            var breakdown = ParseCategoryBreakdown(snapshot.CategoryBreakdownJson);
            foreach (var category in breakdown)
            {
                if (!categories.TryGetValue(category.CategoryName, out var existing))
                {
                    existing = (0m, 0m);
                    categories[category.CategoryName] = existing;
                }

                existing = (existing.Budgeted + category.BudgetedAmount, existing.Actual + category.ActualAmount);
                categories[category.CategoryName] = existing;
            }
        }

        return categories.Select(kv =>
        {
            var consumptionPercent = kv.Value.Budgeted > 0 ? (double)(kv.Value.Actual / kv.Value.Budgeted) * 100 : 0;

            string status;
            if (consumptionPercent > 100)
                status = "Over Budget";
            else if (consumptionPercent > timeElapsedPercent * 1.1)
                status = "Fast";
            else if (consumptionPercent < timeElapsedPercent * 0.9)
                status = "Slow";
            else
                status = "On Track";

            return new CategoryVelocityDto
            {
                CategoryName = kv.Key,
                BudgetedAmount = kv.Value.Budgeted,
                ActualAmount = kv.Value.Actual,
                ConsumptionPercent = Math.Round(consumptionPercent, 1),
                TimeElapsedPercent = timeElapsedPercent,
                Status = status
            };
        }).OrderByDescending(c => c.ConsumptionPercent).ToList();
    }

    private List<(string CategoryName, decimal BudgetedAmount, decimal ActualAmount)>
        ParseCategoryBreakdown(string json)
    {
        var categories = new List<(string CategoryName, decimal BudgetedAmount, decimal ActualAmount)>();

        if (string.IsNullOrWhiteSpace(json) || json == "[]") return categories;

        try
        {
            var items = JsonSerializer.Deserialize<List<CategoryJsonItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items != null)
                foreach (var item in items)
                    categories.Add((item.CategoryName, item.BudgetedAmount, item.ActualAmount));
        }
        catch
        {
            // If JSON parsing fails, return empty list
        }

        return categories;
    }

    private string GetMonthName(int monthNumber)
    {
        return monthNumber switch
        {
            1 => "January",
            2 => "February",
            3 => "March",
            4 => "April",
            5 => "May",
            6 => "June",
            7 => "July",
            8 => "August",
            9 => "September",
            10 => "October",
            11 => "November",
            12 => "December",
            _ => string.Empty
        };
    }

    private string GetCategoryStatus(double percentageUsed)
    {
        if (percentageUsed > 100) return "Over Budget";

        if (percentageUsed >= 90) return "Near Limit";

        return "Within Budget";
    }

    #endregion
}

/// <summary>
///     Internal JSON deserialization helper for category breakdown data.
/// </summary>
internal sealed class CategoryJsonItem
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
}