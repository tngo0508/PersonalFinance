using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Budget month reader implementation that reads synced Google Drive spreadsheet snapshots and transaction records.
/// </summary>
public class SheetBudgetMonthReader : IBudgetMonthReader
{
    private readonly AppDbContext _dbContext;

    public SheetBudgetMonthReader(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default)
    {
        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .OrderBy(s => s.MonthNumber)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        if (snapshots.Count == 0)
        {
            return new List<BudgetMonthData>();
        }

        var result = new List<BudgetMonthData>();
        var grouped = snapshots.GroupBy(s => s.MonthNumber).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var aggregated = AggregateSnapshotsToMonthData(year, group.Key, group.ToList());
            result.Add(aggregated);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month,
        CancellationToken ct = default)
    {
        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == month)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        if (snapshots.Count == 0)
        {
            return null;
        }

        return AggregateSnapshotsToMonthData(year, month, snapshots);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year,
        CancellationToken ct = default)
    {
        return await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .OrderBy(s => s.MonthNumber)
            .ThenBy(s => s.Id)
            .Select(s => new BudgetTrendRecordData
            {
                MonthNumber = s.MonthNumber,
                MonthName = s.MonthName,
                Income = s.ActualIncome,
                Expenses = s.ActualExpenses
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTransactionData>> GetTransactionsAsync(string userId, DateOnly from, DateOnly to,
        int? take = null, CancellationToken ct = default)
    {
        var fromDateTime = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        IQueryable<SpreadsheetTransactionRecord> query = _dbContext.SpreadsheetTransactionRecords
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= fromDateTime);

        if (to != DateOnly.MaxValue)
        {
            var toDateTimeExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(t => t.Date < toDateTimeExclusive);
        }

        query = query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        var records = await query.ToListAsync(ct);

        return records.Select(t => new BudgetTransactionData
        {
            Id = t.Id,
            Description = t.Description,
            Category = t.Category,
            Amount = t.Amount,
            Date = t.Date,
            Type = t.Type
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken ct = default)
    {
        return await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);
    }

    private static BudgetMonthData AggregateSnapshotsToMonthData(int year, int month,
        List<SpreadsheetBudgetSnapshot> snapshots)
    {
        var monthName = snapshots.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.MonthName))?.MonthName;
        if (string.IsNullOrWhiteSpace(monthName))
        {
            monthName = month >= 1 && month <= 12
                ? CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)
                : string.Empty;
        }

        var categoryMap = new Dictionary<string, BudgetMonthCategoryData>(StringComparer.OrdinalIgnoreCase);
        var categoryList = new List<BudgetMonthCategoryData>();

        foreach (var snapshot in snapshots)
        {
            var parsed = ParseCategories(snapshot.CategoryBreakdownJson);
            foreach (var cat in parsed)
            {
                if (categoryMap.TryGetValue(cat.Name, out var existing))
                {
                    existing.Budgeted += cat.Budgeted;
                    existing.Actual += cat.Actual;
                }
                else
                {
                    var newCat = new BudgetMonthCategoryData
                    {
                        Name = cat.Name,
                        Budgeted = cat.Budgeted,
                        Actual = cat.Actual
                    };
                    categoryMap[cat.Name] = newCat;
                    categoryList.Add(newCat);
                }
            }
        }

        var hasStartingBalance = snapshots.Any(s => s.StartingBalance.HasValue);
        var hasEndingBalance = snapshots.Any(s => s.EndingBalance.HasValue);

        return new BudgetMonthData
        {
            Year = year,
            Month = month,
            MonthName = monthName,
            SourceRecordCount = snapshots.Count,
            BudgetedIncome = snapshots.Sum(s => s.BudgetedIncome),
            ActualIncome = snapshots.Sum(s => s.ActualIncome),
            BudgetedExpenses = snapshots.Sum(s => s.BudgetedExpenses),
            ActualExpenses = snapshots.Sum(s => s.ActualExpenses),
            StartingBalance = hasStartingBalance ? snapshots.Sum(s => s.StartingBalance ?? 0m) : null,
            EndingBalance = hasEndingBalance ? snapshots.Sum(s => s.EndingBalance ?? 0m) : null,
            Categories = categoryList
        };
    }

    private static List<BudgetMonthCategoryData> ParseCategories(string json)
    {
        var categories = new List<BudgetMonthCategoryData>();

        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return categories;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<CategoryJsonItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items != null)
            {
                foreach (var item in items)
                {
                    categories.Add(new BudgetMonthCategoryData
                    {
                        Name = item.CategoryName,
                        Budgeted = item.BudgetedAmount,
                        Actual = item.ActualAmount,
                        Kind = BudgetCategoryKind.Expense
                    });
                }
            }
        }
        catch
        {
            // If JSON parsing fails, return empty list
        }

        return categories;
    }
}
