using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Unified provider for budget data that delegates to either the Google Sheet or Budget Tracker reader
/// based on the user's active preference. Never falls back to the other data source.
/// </summary>
public class BudgetDataProvider : IBudgetDataProvider
{
    private readonly Dictionary<string, BudgetDataSource> _cachedSources = new();
    private readonly IBudgetDataSourceService _dataSourceService;
    private readonly SheetBudgetMonthReader _sheetReader;
    private readonly TrackerBudgetMonthReader _trackerReader;

    public BudgetDataProvider(
        IBudgetDataSourceService dataSourceService,
        SheetBudgetMonthReader sheetReader,
        TrackerBudgetMonthReader trackerReader)
    {
        _dataSourceService = dataSourceService;
        _sheetReader = sheetReader;
        _trackerReader = trackerReader;
        _dataSourceService.PreferenceChanged += userId => _cachedSources.Remove(userId);
    }

    /// <inheritdoc />
    public async Task<BudgetDataSource> GetActiveSourceAsync(string userId, CancellationToken ct = default)
    {
        if (_cachedSources.TryGetValue(userId, out BudgetDataSource cached))
        {
            return cached;
        }

        BudgetDataSource source = await _dataSourceService.GetActiveSourceAsync(userId, ct);
        _cachedSources[userId] = source;
        return source;
    }

    /// <inheritdoc />
    public async Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default)
    {
        IBudgetMonthReader reader = await GetReaderAsync(userId, ct);
        return await reader.GetMonthsAsync(userId, year, ct);
    }

    /// <inheritdoc />
    public async Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month,
        CancellationToken ct = default)
    {
        IBudgetMonthReader reader = await GetReaderAsync(userId, ct);
        return await reader.GetMonthAsync(userId, year, month, ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year,
        CancellationToken ct = default)
    {
        IBudgetMonthReader reader = await GetReaderAsync(userId, ct);
        return await reader.GetTrendRecordsAsync(userId, year, ct);
    }

    /// <inheritdoc />
    public async Task<List<BudgetTransactionData>> GetTransactionsAsync(string userId, DateOnly from, DateOnly to,
        int? take = null, CancellationToken ct = default)
    {
        IBudgetMonthReader reader = await GetReaderAsync(userId, ct);
        return await reader.GetTransactionsAsync(userId, from, to, take, ct);
    }

    /// <inheritdoc />
    public async Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken ct = default)
    {
        IBudgetMonthReader reader = await GetReaderAsync(userId, ct);
        return await reader.GetAvailableYearsAsync(userId, ct);
    }

    private async Task<IBudgetMonthReader> GetReaderAsync(string userId, CancellationToken ct)
    {
        BudgetDataSource source = await GetActiveSourceAsync(userId, ct);
        return source == BudgetDataSource.BudgetTracker
            ? _trackerReader
            : _sheetReader;
    }
}
