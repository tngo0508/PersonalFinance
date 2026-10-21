using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Per-category normalized budget amount and actual spending/income.
/// </summary>
public sealed class BudgetMonthCategoryData
{
    public string Name { get; set; } = string.Empty;
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
    public BudgetCategoryKind Kind { get; set; } = BudgetCategoryKind.Expense;
}

/// <summary>
/// Normalized monthly budget data model returned by budget month readers.
/// </summary>
public sealed class BudgetMonthData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;

    /// <summary>Number of source records represented by this month: synced snapshots for Google Sheet, one available month for Budget Tracker.</summary>
    public int SourceRecordCount { get; set; }

    public decimal BudgetedIncome { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal BudgetedExpenses { get; set; }
    public decimal ActualExpenses { get; set; }
    public decimal? StartingBalance { get; set; }
    public decimal? EndingBalance { get; set; }
    public List<BudgetMonthCategoryData> Categories { get; set; } = new();
}

/// <summary>
/// A source's trend observation: one per synced snapshot for Google Sheet, one per available month for Budget Tracker.
/// </summary>
public sealed class BudgetTrendRecordData
{
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetSavings => Income - Expenses;

    public double SavingsRate => Income > 0
        ? Math.Round((double)(NetSavings / Income) * 100, 1)
        : 0;
}

/// <summary>
/// Normalized budget transaction data model returned by budget month readers.
/// </summary>
public sealed class BudgetTransactionData
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = "Expense"; // "Expense" or "Income"
}

/// <summary>
/// Common reader interface for retrieving budget months and transactions in a normalized format.
/// </summary>
public interface IBudgetMonthReader
{
    /// <summary>
    /// Gets all available budget months for a user in the given year.
    /// </summary>
    Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific budget month for a user, or null if no data exists.
    /// </summary>
    Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Gets trend observations ordered by month. Sheet snapshots in the same month remain separate observations.
    /// </summary>
    Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year, CancellationToken ct = default);

    /// <summary>
    /// Gets transactions for a user within a specified date range.
    /// </summary>
    Task<List<BudgetTransactionData>> GetTransactionsAsync(string userId, DateOnly from, DateOnly to, int? take = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all available years that have budget data for the user.
    /// </summary>
    Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Unified budget data provider that delegates to the appropriate reader according to user preference.
/// </summary>
public interface IBudgetDataProvider : IBudgetMonthReader
{
    /// <summary>
    /// Resolves and returns the user's currently active budget data source.
    /// </summary>
    Task<BudgetDataSource> GetActiveSourceAsync(string userId, CancellationToken ct = default);
}
