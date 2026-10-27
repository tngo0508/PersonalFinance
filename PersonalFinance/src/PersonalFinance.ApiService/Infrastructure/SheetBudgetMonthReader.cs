using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Budget month reader implementation that reads synced Google Drive spreadsheet snapshots and transaction records.
/// Every figure it returns is in the Account's Reporting Currency: a snapshot converted at its month's rate, a
/// transaction at its own day's (ADR 0016).
/// </summary>
public class SheetBudgetMonthReader : IBudgetMonthReader
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly ICurrencyConverter _currencyConverter;

    public SheetBudgetMonthReader(AppDbContext dbContext, ICurrencyConverter currencyConverter)
    {
        _dbContext = dbContext;
        _currencyConverter = currencyConverter;
    }

    /// <inheritdoc />
    public async Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default)
    {
        List<SpreadsheetBudgetSnapshot> snapshots = await _dbContext.SpreadsheetBudgetSnapshots
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
        IOrderedEnumerable<IGrouping<int, SpreadsheetBudgetSnapshot>> grouped = snapshots.GroupBy(s => s.MonthNumber).OrderBy(g => g.Key);

        foreach (IGrouping<int, SpreadsheetBudgetSnapshot>? group in grouped)
        {
            BudgetMonthData aggregated = await AggregateAsync(userId, year, group.Key, group.ToList(), ct);
            result.Add(aggregated);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month,
        CancellationToken ct = default)
    {
        List<SpreadsheetBudgetSnapshot> snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year && s.MonthNumber == month)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        if (snapshots.Count == 0)
        {
            return null;
        }

        return await AggregateAsync(userId, year, month, snapshots, ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year,
        CancellationToken ct = default)
    {
        var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Year == year)
            .OrderBy(s => s.MonthNumber)
            .ThenBy(s => s.Id)
            .Select(s => new { s.MonthNumber, s.MonthName, s.ActualIncome, s.ActualExpenses, s.CurrencyCode })
            .ToListAsync(ct);
        ReportingConversion conversion = await ConversionAsync(userId,
            snapshots.Select(s => new MoneyAt(s.CurrencyCode, MonthStart(year, s.MonthNumber))), ct);

        return snapshots
            .Select(s => new BudgetTrendRecordData
            {
                MonthNumber = s.MonthNumber,
                MonthName = s.MonthName,
                Income = conversion.ConvertOrOriginal(s.ActualIncome, s.CurrencyCode, MonthStart(year, s.MonthNumber)),
                Expenses = conversion.ConvertOrOriginal(s.ActualExpenses, s.CurrencyCode, MonthStart(year, s.MonthNumber))
            })
            .ToList();
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

        List<SpreadsheetTransactionRecord> records = await query.ToListAsync(ct);
        ReportingConversion conversion = await ConversionAsync(userId,
            records.Select(t => new MoneyAt(t.CurrencyCode, DateOnly.FromDateTime(t.Date))), ct);

        return records.Select(t => new BudgetTransactionData
        {
            Id = t.Id,
            Description = t.Description,
            Category = t.Category,
            Amount = t.Amount,
            CurrencyCode = t.CurrencyCode,
            ReportingAmount = conversion.Convert(t.Amount, t.CurrencyCode, DateOnly.FromDateTime(t.Date)),
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

    /// <summary>A conversion into the Account's Reporting Currency, prepared for <paramref name="amounts"/>.</summary>
    private async Task<ReportingConversion> ConversionAsync(string userId, IEnumerable<MoneyAt> amounts, CancellationToken ct)
    {
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, ct);
        return await _currencyConverter.PrepareAsync(reportingCurrency, amounts, ct);
    }

    /// <summary>The first day of a snapshot's month: the day its figures convert at (a month number outside 1-12 reads as January).</summary>
    private static DateOnly MonthStart(int year, int month) => new(year, Math.Clamp(month, 1, 12), 1);

    private async Task<BudgetMonthData> AggregateAsync(string userId, int year, int month,
        List<SpreadsheetBudgetSnapshot> snapshots, CancellationToken ct)
    {
        // Each snapshot converts at the month's rate; a month synced from two spreadsheets in different currencies adds up.
        DateOnly monthStart = MonthStart(year, month);
        ReportingConversion conversion = await ConversionAsync(userId,
            snapshots.Select(s => new MoneyAt(s.CurrencyCode, monthStart)), ct);
        decimal Money(SpreadsheetBudgetSnapshot snapshot, decimal amount) =>
            conversion.ConvertOrOriginal(amount, snapshot.CurrencyCode, monthStart);

        string? monthName = snapshots.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.MonthName))?.MonthName;
        if (string.IsNullOrWhiteSpace(monthName))
        {
            monthName = month >= 1 && month <= 12
                ? CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)
                : string.Empty;
        }

        var categoryMap = new Dictionary<string, BudgetMonthCategoryData>(StringComparer.OrdinalIgnoreCase);
        var categoryList = new List<BudgetMonthCategoryData>();

        foreach (SpreadsheetBudgetSnapshot snapshot in snapshots)
        {
            List<BudgetMonthCategoryData> parsed = ParseCategories(snapshot.CategoryBreakdownJson);
            foreach (BudgetMonthCategoryData cat in parsed)
            {
                if (categoryMap.TryGetValue(cat.Name, out BudgetMonthCategoryData? existing))
                {
                    existing.Budgeted += Money(snapshot, cat.Budgeted);
                    existing.Actual += Money(snapshot, cat.Actual);
                }
                else
                {
                    var newCat = new BudgetMonthCategoryData
                    {
                        Name = cat.Name,
                        Budgeted = Money(snapshot, cat.Budgeted),
                        Actual = Money(snapshot, cat.Actual)
                    };
                    categoryMap[cat.Name] = newCat;
                    categoryList.Add(newCat);
                }
            }
        }

        bool hasStartingBalance = snapshots.Any(s => s.StartingBalance.HasValue);
        bool hasEndingBalance = snapshots.Any(s => s.EndingBalance.HasValue);

        return new BudgetMonthData
        {
            Year = year,
            Month = month,
            MonthName = monthName,
            CurrencyCode = conversion.ReportingCurrency,
            Conversion = conversion.ToDto(),
            SourceRecordCount = snapshots.Count,
            BudgetedIncome = conversion.Total(snapshots.Select(s => Money(s, s.BudgetedIncome))),
            ActualIncome = conversion.Total(snapshots.Select(s => Money(s, s.ActualIncome))),
            BudgetedExpenses = conversion.Total(snapshots.Select(s => Money(s, s.BudgetedExpenses))),
            ActualExpenses = conversion.Total(snapshots.Select(s => Money(s, s.ActualExpenses))),
            StartingBalance = hasStartingBalance ? conversion.Total(snapshots.Select(s => Money(s, s.StartingBalance ?? 0m))) : null,
            EndingBalance = hasEndingBalance ? conversion.Total(snapshots.Select(s => Money(s, s.EndingBalance ?? 0m))) : null,
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
            List<CategoryJsonItem>? items = JsonSerializer.Deserialize<List<CategoryJsonItem>>(json, CaseInsensitiveJsonOptions);

            if (items != null)
            {
                foreach (CategoryJsonItem item in items)
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
