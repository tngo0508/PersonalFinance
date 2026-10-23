using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     Implementation of ISpreadsheetAnalysisService that queries the database
///     to provide analysis of synced spreadsheet budget data.
/// </summary>
internal sealed class SpreadsheetAnalysisService : ISpreadsheetAnalysisService
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
            BudgetMonthData? monthData = await _budgetDataProvider.GetMonthAsync(userId, year, month.Value, cancellationToken);
            if (monthData == null) return null;

            decimal totalIncome = monthData.ActualIncome;
            decimal totalExpenses = monthData.ActualExpenses;
            decimal netSavings = totalIncome - totalExpenses;
            decimal savingsRate = FinanceMetrics.ActualSavingsRatePercent(totalIncome, totalExpenses);

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
            List<BudgetMonthData> months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
            if (months.Count == 0) return null;

            decimal totalIncome = months.Sum(m => m.ActualIncome);
            decimal totalExpenses = months.Sum(m => m.ActualExpenses);
            decimal netSavings = totalIncome - totalExpenses;
            decimal savingsRate = FinanceMetrics.ActualSavingsRatePercent(totalIncome, totalExpenses);
            int firstMonth = months.FirstOrDefault()?.Month ?? 0;
            int snapshotCount = months.Sum(m => m.SourceRecordCount);

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
        int targetMonth = month ?? await GetCurrentMonthForVelocityAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        BudgetMonthData? monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

        DateTime now = DateTime.UtcNow;
        bool isCurrentMonth = now.Year == year && now.Month == targetMonth;

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

        decimal totalExpenses = monthData.ActualExpenses;
        decimal budgetedExpenses = monthData.BudgetedExpenses;

        decimal dailyBurnRate = daysElapsed > 0 ? totalExpenses / (decimal)daysElapsed : 0;
        decimal projectedMonthlySpend = daysInMonth > 0 ? dailyBurnRate * daysInMonth : 0;

        double budgetConsumptionPercent = budgetedExpenses > 0 ? (double)(totalExpenses / budgetedExpenses) * 100 : 0;
        double timeElapsedPercent = daysInMonth > 0 ? daysElapsed / daysInMonth * 100 : 0;

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
        List<WeeklySpendingDto> weeklyTrend = await CalculateWeeklyTrendAsync(userId, year, targetMonth, cancellationToken);

        // Calculate category velocities
        List<CategoryVelocityDto> categoryVelocities = CalculateCategoryVelocities(monthData, timeElapsedPercent);

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
        List<BudgetTrendRecordData> records = await _budgetDataProvider.GetTrendRecordsAsync(userId, year, cancellationToken);

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
        int targetMonth = month ?? await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        BudgetMonthData? monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

        var categoryList = monthData.Categories.Select(c =>
        {
            decimal variance = c.Budgeted - c.Actual;
            double percentageUsed = c.Budgeted > 0
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

        decimal totalSpent = categoryList.Sum(c => c.ActualAmount);
        decimal totalBudgeted = categoryList.Sum(c => c.BudgetedAmount);

        return new CategoryInsightsDto
        {
            Year = year,
            MonthNumber = targetMonth,
            MonthName = GetMonthName(targetMonth),
            Categories = categoryList,
            TotalSpent = totalSpent,
            TotalBudgeted = totalBudgeted,
            ActualIncome = monthData.ActualIncome,
            ActualExpenses = monthData.ActualExpenses
        };
    }

    public async Task<BudgetVarianceDto?> GetBudgetVarianceAsync(string userId, int year, int? month = null,
        CancellationToken cancellationToken = default)
    {
        int targetMonth = month ?? await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
        if (targetMonth == 0) return null;

        BudgetMonthData? monthData = await _budgetDataProvider.GetMonthAsync(userId, year, targetMonth, cancellationToken);
        if (monthData == null) return null;

        var rankings = monthData.Categories.Select((c, index) =>
        {
            decimal variance = c.Budgeted - c.Actual;
            double variancePercent = c.Budgeted > 0 ? (double)(variance / c.Budgeted) * 100 : 0;

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
        for (int i = 0; i < rankings.Count; i++) rankings[i].Rank = i + 1;

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
        List<BudgetMonthData> months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
        return months.Select(m => m.Month).Distinct().OrderBy(m => m).ToList();
    }

    public async Task<(int year, int month)?> GetMostRecentMonthAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        List<int> years = await _budgetDataProvider.GetAvailableYearsAsync(userId, cancellationToken);
        foreach (int year in years.OrderByDescending(y => y))
        {
            List<BudgetMonthData> months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
            if (months.Count > 0)
            {
                int latestMonth = months.Max(m => m.Month);
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

    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: Analysis dashboard.")]
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
            GoogleDriveConnection? connection = await _dbContext.GoogleDriveConnections
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
            foreach (MonthlyBudgetMonthSummaryDto month in budgetReport.Months)
            {
                bool existing = await _dbContext.SpreadsheetBudgetSnapshots
                    .AnyAsync(s => s.ConnectionId == connectionId
                                   && s.Year == budgetReport.Year
                                   && s.MonthNumber == month.MonthNumber, cancellationToken);

                if (existing)
                {
                    SpreadsheetBudgetSnapshot? existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
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

            if (conflicts.Count > 0 && !overwrite)
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    HasConflict = true,
                    Conflicts = conflicts,
                    ErrorMessage = "Some months already exist in the database. Confirm overwrite to proceed."
                };

            if (overwrite)
            {
                foreach (MonthConflictInfo conflict in conflicts)
                {
                    SpreadsheetBudgetSnapshot? existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
                        .FirstOrDefaultAsync(s => s.Id == conflict.SnapshotId, cancellationToken);

                    if (existingSnapshot != null) _dbContext.SpreadsheetBudgetSnapshots.Remove(existingSnapshot);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            int snapshotsCreated = 0;
            foreach (MonthlyBudgetMonthSummaryDto month in budgetReport.Months)
            {
                string categoryJson = JsonSerializer.Serialize(
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

            int transactionsCreated = 0;
            Dictionary<int, SpreadsheetBudgetSnapshot> snapshots = await _dbContext.SpreadsheetBudgetSnapshots
                .Where(s => s.ConnectionId == connectionId
                            && s.FileId == fileId
                            && s.Year == budgetReport.Year)
                .ToDictionaryAsync(s => s.MonthNumber, cancellationToken);

            foreach (SpreadsheetTransactionDto transaction in transactionsDto.Transactions)
            {
                if (!transaction.Date.HasValue) continue;

                DateTime date = transaction.Date.Value;
                int monthNumber = date.Month;

                if (!snapshots.TryGetValue(monthNumber, out SpreadsheetBudgetSnapshot? snapshot)) continue;

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
        DateTime now = DateTime.UtcNow;
        if (now.Year == year) return now.Month;

        return await GetMostRecentMonthNumberAsync(userId, year, cancellationToken);
    }

    private async Task<int> GetMostRecentMonthNumberAsync(string userId, int year, CancellationToken cancellationToken)
    {
        List<BudgetMonthData> months = await _budgetDataProvider.GetMonthsAsync(userId, year, cancellationToken);
        return months.Count > 0 ? months.Max(m => m.Month) : 0;
    }

    private async Task<List<WeeklySpendingDto>> CalculateWeeklyTrendAsync(string userId, int year, int month,
        CancellationToken cancellationToken)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var fromDate = new DateOnly(year, month, 1);
        var toDate = new DateOnly(year, month, daysInMonth);

        List<BudgetTransactionData> transactions = await _budgetDataProvider.GetTransactionsAsync(
            userId, fromDate, toDate, null, cancellationToken);

        var expenseTransactions = transactions
            .Where(t => string.Equals(t.Type, "Expense", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Date)
            .ToList();

        if (expenseTransactions.Count == 0) return new List<WeeklySpendingDto>();

        int weeksInMonth = (int)Math.Ceiling(daysInMonth / 7.0);

        var weeklyData = new List<WeeklySpendingDto>();

        for (int week = 1; week <= weeksInMonth; week++)
        {
            int startDay = (week - 1) * 7 + 1;
            int endDay = Math.Min(week * 7, daysInMonth);

            IEnumerable<BudgetTransactionData> weekTransactions = expenseTransactions.Where(t => t.Date.Day >= startDay && t.Date.Day <= endDay);
            decimal totalSpent = weekTransactions.Sum(t => t.Amount);

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
            double consumptionPercent = c.Budgeted > 0 ? (double)(c.Actual / c.Budgeted) * 100 : 0;

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

    private static string GetMonthName(int monthNumber)
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

    private static string GetCategoryStatus(double percentageUsed)
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
