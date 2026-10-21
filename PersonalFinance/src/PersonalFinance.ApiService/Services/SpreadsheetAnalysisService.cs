using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Services;

/// <summary>
///     Implementation of ISpreadsheetAnalysisService that queries the database
///     to provide analysis of synced spreadsheet budget data.
/// </summary>
public class SpreadsheetAnalysisService : ISpreadsheetAnalysisService
{
    private readonly IBudgetDataProvider _budgetDataProvider;
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ISpreadsheetReader _spreadsheetReader;

    public SpreadsheetAnalysisService(AppDbContext dbContext, ISpreadsheetReader spreadsheetReader,
        INotificationService notificationService, IBudgetDataProvider budgetDataProvider)
    {
        _dbContext = dbContext;
        _spreadsheetReader = spreadsheetReader;
        _notificationService = notificationService;
        _budgetDataProvider = budgetDataProvider;
    }

    public async Task<SummaryStatisticsDto?> GetSummaryStatisticsAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        if (month.HasValue)
        {
            var monthData = await _budgetDataProvider.GetMonthAsync(userId, year, month.Value, cancellationToken);
            if (monthData == null) return null;

            var totalIncome = monthData.ActualIncome;
            var totalExpenses = monthData.ActualExpenses;
            var netSavings = totalIncome - totalExpenses;
            var savingsRate = FinanceMetrics.ActualSavingsRatePercent(totalIncome, totalExpenses);

            return new SummaryStatisticsDto
            {
                Year = year,
                MonthNumber = monthData.Month,
                MonthName = GetMonthName(monthData.Month),
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                NetSavings = netSavings,
                SavingsRate = savingsRate,
                SnapshotCount = monthData.SourceRecordCount
            };
        }
        else
        {
            var months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
            if (!months.Any()) return null;

            var totalIncome = months.Sum(m => m.ActualIncome);
            var totalExpenses = months.Sum(m => m.ActualExpenses);
            var netSavings = totalIncome - totalExpenses;
            var savingsRate = FinanceMetrics.ActualSavingsRatePercent(totalIncome, totalExpenses);
            var firstMonth = months.FirstOrDefault()?.Month ?? 0;
            var snapshotCount = months.Sum(m => m.SourceRecordCount);

            return new SummaryStatisticsDto
            {
                Year = year,
                MonthNumber = firstMonth,
                MonthName = GetMonthName(firstMonth),
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                NetSavings = netSavings,
                SavingsRate = savingsRate,
                SnapshotCount = snapshotCount
            };
        }
    }

    public async Task<SpendingVelocityDto?> GetSpendingVelocityAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        var targetMonth = month ?? await GetCurrentMonthForVelocityAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        var monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

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

        var totalExpenses = monthData.ActualExpenses;
        var budgetedExpenses = monthData.BudgetedExpenses;

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
        var categoryVelocities = CalculateCategoryVelocities(monthData, timeElapsedPercent);

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
        var records = await _budgetDataProvider.GetTrendRecordsAsync(userId, year, cancellationToken);

        var trendPoints = records.Select(r => new MonthlyTrendPointDto
        {
            MonthNumber = r.MonthNumber,
            MonthName = r.MonthName,
            Income = r.Income,
            Expenses = r.Expenses,
            NetSavings = r.NetSavings,
            SavingsRate = r.SavingsRate
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

        var monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

        var categoryList = monthData.Categories.Select(c =>
        {
            var variance = c.Budgeted - c.Actual;
            var percentageUsed = c.Budgeted > 0
                ? Math.Round((double)(c.Actual / c.Budgeted) * 100, 1)
                : 0;
            return new CategoryBreakdownDto
            {
                CategoryName = c.Name,
                BudgetedAmount = c.Budgeted,
                ActualAmount = c.Actual,
                Variance = variance,
                PercentageUsed = percentageUsed,
                Status = GetCategoryStatus(percentageUsed)
            };
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

        var monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

        var rankings = monthData.Categories.Select((c, index) =>
        {
            var variance = c.Budgeted - c.Actual;
            var variancePercent = c.Budgeted > 0 ? (double)(variance / c.Budgeted) * 100 : 0;

            return new VarianceRankingDto
            {
                Rank = index + 1,
                CategoryName = c.Name,
                BudgetedAmount = c.Budgeted,
                ActualAmount = c.Actual,
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
        return await _budgetDataProvider.GetAvailableYearsAsync(userId, cancellationToken);
    }

    public async Task<List<int>> GetAvailableMonthsAsync(string userId, int year,
        CancellationToken cancellationToken = default)
    {
        var months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
        return months.Select(m => m.Month).Distinct().OrderBy(m => m).ToList();
    }

    public async Task<(int year, int month)?> GetMostRecentMonthAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var years = await _budgetDataProvider.GetAvailableYearsAsync(userId, cancellationToken);
        foreach (var year in years.OrderByDescending(y => y))
        {
            var months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
            if (months.Any())
            {
                var latestMonth = months.Max(m => m.Month);
                return (year, latestMonth);
            }
        }

        return null;
    }

    public async Task<BudgetDataSource> GetActiveSourceAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return await _budgetDataProvider.GetActiveSourceAsync(userId, cancellationToken);
    }

    public async Task<SyncSpreadsheetBudgetResult> SyncSpreadsheetBudgetAsync(
        string userId,
        int connectionId,
        string fileId,
        string fileName,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _dbContext.GoogleDriveConnections
                .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

            if (connection == null)
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    ErrorMessage = "Google Drive connection not found or access denied."
                };

            MonthlyBudgetReportDto budgetReport;
            try
            {
                budgetReport = await _spreadsheetReader.GetSpreadsheetBudgetReportAsync(
                    userId, fileId, fileName, connectionId, cancellationToken);
            }
            catch (Exception ex)
            {
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to read spreadsheet: {ex.Message}"
                };
            }

            // Only months actually read from the sheet are ever saved as snapshots.
            if (budgetReport.Months.Count == 0)
            {
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    ErrorMessage = budgetReport.ErrorMessage ?? "No budget data could be read from the spreadsheet."
                };
            }

            var conflicts = new List<MonthConflictInfo>();
            foreach (var month in budgetReport.Months)
            {
                var existing = await _dbContext.SpreadsheetBudgetSnapshots
                    .AnyAsync(s => s.ConnectionId == connectionId
                                   && s.Year == budgetReport.Year
                                   && s.MonthNumber == month.MonthNumber, cancellationToken);

                if (existing)
                {
                    var existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
                        .FirstOrDefaultAsync(s => s.ConnectionId == connectionId
                                                  && s.Year == budgetReport.Year
                                                  && s.MonthNumber == month.MonthNumber, cancellationToken);

                    if (existingSnapshot != null)
                        conflicts.Add(new MonthConflictInfo
                        {
                            SnapshotId = existingSnapshot.Id,
                            MonthNumber = month.MonthNumber,
                            MonthName = month.MonthName,
                            Year = budgetReport.Year,
                            SyncedAtUtc = existingSnapshot.SyncedAtUtc
                        });
                }
            }

            if (conflicts.Any() && !overwrite)
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    HasConflict = true,
                    Conflicts = conflicts,
                    ErrorMessage = "Some months already exist in the database. Confirm overwrite to proceed."
                };

            if (overwrite)
            {
                foreach (var conflict in conflicts)
                {
                    var existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
                        .FirstOrDefaultAsync(s => s.Id == conflict.SnapshotId, cancellationToken);

                    if (existingSnapshot != null) _dbContext.SpreadsheetBudgetSnapshots.Remove(existingSnapshot);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            var snapshotsCreated = 0;
            foreach (var month in budgetReport.Months)
            {
                var categoryJson = JsonSerializer.Serialize(
                    month.Categories.Select(c => new
                    {
                        c.CategoryName,
                        c.BudgetedAmount,
                        c.ActualAmount
                    }).ToList());

                var snapshot = new SpreadsheetBudgetSnapshot
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    FileName = budgetReport.FileName,
                    FileId = fileId,
                    MonthNumber = month.MonthNumber,
                    MonthName = month.MonthName,
                    Year = budgetReport.Year,
                    BudgetedIncome = month.BudgetedIncome,
                    ActualIncome = month.ActualIncome,
                    BudgetedExpenses = month.BudgetedExpenses,
                    ActualExpenses = month.ActualExpenses,
                    StartingBalance = month.StartingBalance,
                    EndingBalance = month.EndingBalance,
                    CategoryBreakdownJson = categoryJson,
                    SyncedAtUtc = DateTime.UtcNow
                };

                _dbContext.SpreadsheetBudgetSnapshots.Add(snapshot);
                snapshotsCreated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            SpreadsheetTransactionsDto transactionsDto;
            try
            {
                transactionsDto = await _spreadsheetReader.GetSpreadsheetTransactionsAsync(
                    userId, fileId, fileName, connectionId, cancellationToken);
            }
            catch
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return new SyncSpreadsheetBudgetResult
                {
                    Success = true,
                    SnapshotsCreated = snapshotsCreated,
                    TransactionsCreated = 0
                };
            }

            var transactionsCreated = 0;
            var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
                .Where(s => s.ConnectionId == connectionId
                            && s.FileId == fileId
                            && s.Year == budgetReport.Year)
                .ToDictionaryAsync(s => s.MonthNumber, cancellationToken);

            foreach (var transaction in transactionsDto.Transactions)
            {
                if (!transaction.Date.HasValue) continue;

                var date = transaction.Date.Value;
                var monthNumber = date.Month;

                if (!snapshots.TryGetValue(monthNumber, out var snapshot)) continue;

                var record = new SpreadsheetTransactionRecord
                {
                    SnapshotId = snapshot.Id,
                    ConnectionId = connectionId,
                    UserId = userId,
                    Date = date,
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    Category = transaction.Category,
                    Type = transaction.Type
                };

                _dbContext.SpreadsheetTransactionRecords.Add(record);
                transactionsCreated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Create success notification
            await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
            {
                Type = "SyncStatus",
                Title = "Google Drive sync completed",
                Message =
                    $"Successfully synced {snapshotsCreated} budget snapshots and {transactionsCreated} transactions from '{fileName}'.",
                SourceFeature = "GoogleDrive"
            }, cancellationToken);

            return new SyncSpreadsheetBudgetResult
            {
                Success = true,
                SnapshotsCreated = snapshotsCreated,
                TransactionsCreated = transactionsCreated
            };
        }
        catch (Exception ex)
        {
            // Create failure notification
            await _notificationService.CreateNotificationAsync(userId, new CreateNotificationRequestDto
            {
                Type = "SyncStatus",
                Title = "Google Drive sync failed",
                Message = $"Sync failed: {ex.Message}",
                SourceFeature = "GoogleDrive"
            }, cancellationToken);

            return new SyncSpreadsheetBudgetResult
            {
                Success = false,
                ErrorMessage = $"Sync failed: {ex.Message}"
            };
        }
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
        var months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
        return months.Any() ? months.Max(m => m.Month) : 0;
    }

    private async Task<List<WeeklySpendingDto>> CalculateWeeklyTrendAsync(string userId, int year, int month,
        CancellationToken cancellationToken)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var fromDate = new DateOnly(year, month, 1);
        var toDate = new DateOnly(year, month, daysInMonth);

        var transactions = await _budgetDataProvider.GetTransactionsAsync(
            userId, fromDate, toDate, null, cancellationToken);

        var expenseTransactions = transactions
            .Where(t => string.Equals(t.Type, "Expense", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Date)
            .ToList();

        if (!expenseTransactions.Any()) return new List<WeeklySpendingDto>();

        var weeksInMonth = (int)Math.Ceiling(daysInMonth / 7.0);

        var weeklyData = new List<WeeklySpendingDto>();

        for (var week = 1; week <= weeksInMonth; week++)
        {
            var startDay = (week - 1) * 7 + 1;
            var endDay = Math.Min(week * 7, daysInMonth);

            var weekTransactions = expenseTransactions.Where(t => t.Date.Day >= startDay && t.Date.Day <= endDay);
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

    private static List<CategoryVelocityDto> CalculateCategoryVelocities(BudgetMonthData monthData,
        double timeElapsedPercent)
    {
        if (monthData.Categories.Count == 0) return new List<CategoryVelocityDto>();

        return monthData.Categories.Select(c =>
        {
            var consumptionPercent = c.Budgeted > 0 ? (double)(c.Actual / c.Budgeted) * 100 : 0;

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
                CategoryName = c.Name,
                BudgetedAmount = c.Budgeted,
                ActualAmount = c.Actual,
                ConsumptionPercent = Math.Round(consumptionPercent, 1),
                TimeElapsedPercent = timeElapsedPercent,
                Status = status
            };
        }).OrderByDescending(c => c.ConsumptionPercent).ToList();
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
