using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Budget month reader implementation that reads user-entered Budget Tracker data (plans, lines, categories, transactions).
/// Every figure it returns is in the Account's Reporting Currency: each line at its month's rate, each transaction at
/// its own day's (ADR 0016).
/// </summary>
public class TrackerBudgetMonthReader : IBudgetMonthReader
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrencyConverter _currencyConverter;

    public TrackerBudgetMonthReader(AppDbContext dbContext, ICurrencyConverter currencyConverter)
    {
        _dbContext = dbContext;
        _currencyConverter = currencyConverter;
    }

    /// <inheritdoc />
    public async Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default)
    {
        if (year < 1900 || year > 2100)
        {
            return new List<BudgetMonthData>();
        }

        List<MonthlyBudgetPlan> plans = await _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.UserId == userId && p.Year == year)
            .ToListAsync(ct);

        var yearStart = new DateOnly(year, 1, 1);
        var nextYearStart = new DateOnly(year + 1, 1, 1);

        List<BudgetTransaction> transactions = await _dbContext.BudgetTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= yearStart && t.Date < nextYearStart)
            .ToListAsync(ct);

        List<BudgetCategory> allCategories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        IEnumerable<int> planMonths = plans.Select(p => p.Month);
        IEnumerable<int> txMonths = transactions.Select(t => t.Date.Month);
        var monthNumbers = planMonths.Union(txMonths).Distinct().OrderBy(m => m).ToList();

        var plansByMonth = plans.ToDictionary(p => p.Month);
        var txsByMonth = transactions.GroupBy(t => t.Date.Month).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BudgetMonthData>();

        foreach (int month in monthNumbers)
        {
            plansByMonth.TryGetValue(month, out MonthlyBudgetPlan? plan);
            txsByMonth.TryGetValue(month, out List<BudgetTransaction>? monthTxs);
            monthTxs ??= new List<BudgetTransaction>();

            BudgetMonthData? monthData = await BuildMonthDataAsync(userId, year, month, plan, monthTxs, allCategories, ct);
            if (monthData != null)
            {
                result.Add(monthData);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month,
        CancellationToken ct = default)
    {
        if (year < 1900 || year > 2100 || month < 1 || month > 12)
        {
            return null;
        }

        MonthlyBudgetPlan? plan = await _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

        var startDate = new DateOnly(year, month, 1);
        DateOnly endDate = startDate.AddMonths(1);

        List<BudgetTransaction> transactions = await _dbContext.BudgetTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= startDate && t.Date < endDate)
            .ToListAsync(ct);

        if (plan == null && transactions.Count == 0)
        {
            return null;
        }

        List<BudgetCategory> allCategories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        return await BuildMonthDataAsync(userId, year, month, plan, transactions, allCategories, ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year,
        CancellationToken ct = default)
    {
        List<BudgetMonthData> months = await GetMonthsAsync(userId, year, ct);
        return months.Select(month => new BudgetTrendRecordData
        {
            MonthNumber = month.Month,
            MonthName = month.MonthName,
            Income = month.ActualIncome,
            Expenses = month.ActualExpenses
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<BudgetTransactionData>> GetTransactionsAsync(string userId, DateOnly from, DateOnly to,
        int? take = null, CancellationToken ct = default)
    {
        IQueryable<BudgetTransaction> query = _dbContext.BudgetTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= from && t.Date <= to)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        List<BudgetTransaction> transactions = await query.ToListAsync(ct);
        if (transactions.Count == 0)
        {
            return new List<BudgetTransactionData>();
        }

        var categoryIds = transactions.Select(t => t.CategoryId).Distinct().ToList();
        Dictionary<int, BudgetCategory> categories = await _dbContext.BudgetCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.UserId == userId && categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);
        ReportingConversion conversion = await ConversionAsync(userId,
            transactions.Select(t => new MoneyAt(t.CurrencyCode, t.Date)), ct);

        return transactions.Select(t =>
        {
            BudgetCategory? cat = categories.TryGetValue(t.CategoryId, out BudgetCategory? c) ? c : null;
            return new BudgetTransactionData
            {
                Id = t.Id,
                Description = t.Description,
                Category = cat?.Name ?? string.Empty,
                Amount = t.Amount,
                CurrencyCode = t.CurrencyCode,
                ReportingAmount = conversion.Convert(t.Amount, t.CurrencyCode, t.Date),
                Date = t.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Type = cat?.Kind == BudgetCategoryKind.Income ? "Income" : "Expense"
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken ct = default)
    {
        List<int> planYears = await _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Year)
            .Distinct()
            .ToListAsync(ct);

        List<int> txYears = await _dbContext.BudgetTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => t.Date.Year)
            .Distinct()
            .ToListAsync(ct);

        return planYears.Union(txYears).Distinct().OrderByDescending(y => y).ToList();
    }

    /// <summary>A conversion into the Account's Reporting Currency, prepared for <paramref name="amounts"/>.</summary>
    private async Task<ReportingConversion> ConversionAsync(string userId, IEnumerable<MoneyAt> amounts, CancellationToken ct)
    {
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, ct);
        return await _currencyConverter.PrepareAsync(reportingCurrency, amounts, ct);
    }

    private async Task<BudgetMonthData?> BuildMonthDataAsync(
        string userId,
        int year,
        int month,
        MonthlyBudgetPlan? plan,
        List<BudgetTransaction> transactions,
        List<BudgetCategory> allCategories,
        CancellationToken ct)
    {
        if (plan == null && transactions.Count == 0)
        {
            return null;
        }

        // Lines at the month's rate, transactions at their own day's (ADR 0016); the calculator sees converted figures.
        var monthStart = new DateOnly(year, month, 1);
        List<MonthlyBudgetLine> storedLines = plan?.Lines.ToList() ?? [];
        ReportingConversion conversion = await ConversionAsync(userId,
            transactions.Select(t => new MoneyAt(t.CurrencyCode, t.Date))
                .Concat(storedLines.Select(l => new MoneyAt(l.CurrencyCode, monthStart))), ct);
        Dictionary<int, MonthlyBudgetLine> lineMap = storedLines.ToDictionary(l => l.CategoryId,
            l => MonthlyBudgetCalculator.InReportingCurrency(l, conversion, monthStart));
        Dictionary<int, decimal> txSumByCategory = MonthlyBudgetCalculator.TransactionSums(transactions, conversion);

        List<CalculatedBudgetCategoryRow> calculatedRows = MonthlyBudgetCalculator.BuildRows(allCategories, lineMap, txSumByCategory);

        var expenseRows = calculatedRows.Where(r => r.Kind == BudgetCategoryKind.Expense).ToList();
        var incomeRows = calculatedRows.Where(r => r.Kind == BudgetCategoryKind.Income).ToList();

        var categoryDataList = expenseRows.Select(r => new BudgetMonthCategoryData
        {
            Name = r.Name,
            Budgeted = r.Planned,
            Actual = r.Actual,
            Kind = BudgetCategoryKind.Expense
        }).ToList();

        string monthName = month >= 1 && month <= 12
            ? CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)
            : string.Empty;

        return new BudgetMonthData
        {
            Year = year,
            Month = month,
            MonthName = monthName,
            CurrencyCode = conversion.ReportingCurrency,
            Conversion = conversion.ToDto(),
            SourceRecordCount = 1,
            BudgetedIncome = conversion.Total(incomeRows.Select(r => r.Planned)),
            ActualIncome = conversion.Total(incomeRows.Select(r => r.Actual)),
            BudgetedExpenses = conversion.Total(expenseRows.Select(r => r.Planned)),
            ActualExpenses = conversion.Total(expenseRows.Select(r => r.Actual)),
            Categories = categoryDataList
        };
    }
}
