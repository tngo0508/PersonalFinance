using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;

namespace PersonalFinance.Tests;

/// <summary>
/// Helper factory for instantiating an IBudgetDataProvider for in-memory database tests.
/// </summary>
public static class TestBudgetDataProviderFactory
{
    public static IBudgetDataProvider Create(AppDbContext db)
    {
        var prefService = new BudgetDataSourceService(db);
        var sheetReader = new SheetBudgetMonthReader(db);
        var trackerReader = new TrackerBudgetMonthReader(db);
        return new BudgetDataProvider(prefService, sheetReader, trackerReader);
    }
}
