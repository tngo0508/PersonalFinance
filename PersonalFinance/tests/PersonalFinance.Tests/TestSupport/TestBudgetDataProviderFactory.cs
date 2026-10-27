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
        ICurrencyConverter converter = TestCurrencyConverter.Create(db);
        var sheetReader = new SheetBudgetMonthReader(db, converter);
        var trackerReader = new TrackerBudgetMonthReader(db, converter);
        return new BudgetDataProvider(prefService, sheetReader, trackerReader);
    }
}
