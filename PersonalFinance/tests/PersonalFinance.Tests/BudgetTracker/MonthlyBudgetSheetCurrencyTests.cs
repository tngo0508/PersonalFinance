using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// The Budget Tracker sheet in the Reporting Currency (ADR 0016): a transaction converts at its own day's rate and a
/// planned line at the month's, each keeping its stored amount and currency beside the figure; totals sum the converted
/// figures and the sheet says which rate did it, or that none could.
/// </summary>
public class MonthlyBudgetSheetCurrencyTests
{
    private const string UserId = "sheet-currency-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sheet_InUsd_ConvertsAVndTransactionAtItsDay_AndKeepsItsOriginal()
    {
        await using AppDbContext db = CreateContext(Rate(new DateOnly(2026, 10, 1), 25_000m), Rate(new DateOnly(2026, 10, 15), 25_800m));
        MonthlyBudgetService service = Service(db);
        int food = await FoodAsync(db, service);
        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 2), Amount = 100m, Description = "Lunch", CategoryId = food
        });
        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 16), Amount = 258_000m, Description = "Phở", CategoryId = food, CurrencyCode = "VND"
        });

        MonthlyBudgetSheetDto sheet = await service.GetSheetAsync(UserId, 2026, 10);

        Assert.Equal("USD", sheet.ReportingCurrency);
        SheetRowDto row = Assert.Single(sheet.Expenses.Rows, r => r.CategoryId == food);
        // 100 USD plus 258,000 VND at the 15 October rate, which stands in for the 16th.
        Assert.Equal(110m, row.Actual);
        Assert.Null(row.OriginalActual);
        Assert.Equal(110m, sheet.Expenses.TotalActual);
        BudgetTransactionDto pho = Assert.Single(sheet.Transactions, t => t.Description == "Phở");
        Assert.Equal(("VND", 258_000m, 10m), (pho.CurrencyCode, pho.Amount, pho.ReportingAmount));
        BudgetTransactionDto lunch = Assert.Single(sheet.Transactions, t => t.Description == "Lunch");
        Assert.Equal(("USD", 100m), (lunch.CurrencyCode, lunch.ReportingAmount));
        Assert.NotNull(sheet.Conversion);
        Assert.True(sheet.Conversion.Converted);
        Assert.False(sheet.Conversion.Unavailable);
        ConversionRateDto rate = Assert.Single(sheet.Conversion.Rates);
        Assert.Equal((new DateOnly(2026, 10, 15), 25_800m, true, false), (rate.Date, rate.Rate, rate.IsDated, rate.IsStale));
    }

    [Fact]
    public async Task Sheet_InUsd_ConvertsAVndPlannedLine_AtTheMonthsFirstDay_AndKeepsItsOriginal()
    {
        await using AppDbContext db = CreateContext(Rate(new DateOnly(2026, 10, 1), 25_000m));
        MonthlyBudgetService service = Service(db);
        int food = await FoodAsync(db, service);
        await service.SetPlannedAsync(UserId, 2026, 10, food, new SetPlannedRequest { Amount = 2_500_000m, CurrencyCode = "VND" });

        MonthlyBudgetSheetDto sheet = await service.GetSheetAsync(UserId, 2026, 10);

        SheetRowDto row = Assert.Single(sheet.Expenses.Rows, r => r.CategoryId == food);
        Assert.Equal(("VND", 100m, 2_500_000m), (row.CurrencyCode, row.Planned, row.OriginalPlanned));
        Assert.Equal(100m, sheet.Expenses.TotalPlanned);
    }

    [Fact]
    public async Task Sheet_WithNoRate_KeepsTheStoredAmounts_AndSaysConversionIsUnavailable()
    {
        await using AppDbContext db = CreateContext();
        MonthlyBudgetService service = Service(db);
        int food = await FoodAsync(db, service);
        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 16), Amount = 258_000m, Description = "Phở", CategoryId = food, CurrencyCode = "VND"
        });

        MonthlyBudgetSheetDto sheet = await service.GetSheetAsync(UserId, 2026, 10);

        SheetRowDto row = Assert.Single(sheet.Expenses.Rows, r => r.CategoryId == food);
        Assert.Equal(258_000m, row.Actual);
        Assert.Equal(("VND", 258_000m), (row.CurrencyCode, row.OriginalActual));
        Assert.Null(Assert.Single(sheet.Transactions).ReportingAmount);
        Assert.NotNull(sheet.Conversion);
        Assert.True(sheet.Conversion.Unavailable);
    }

    [Fact]
    public async Task Sheet_ReportingInVnd_ConvertsUsdLines_AndRoundsTotalsToWholeDong()
    {
        await using AppDbContext db = CreateContext(Rate(new DateOnly(2026, 10, 1), 25_800m));
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "en", ReportingCurrency = "VND" });
        await db.SaveChangesAsync();
        MonthlyBudgetService service = Service(db);
        int food = await FoodAsync(db, service);
        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 2), Amount = 10.5m, Description = "Lunch", CategoryId = food
        });

        MonthlyBudgetSheetDto sheet = await service.GetSheetAsync(UserId, 2026, 10);

        Assert.Equal("VND", sheet.ReportingCurrency);
        SheetRowDto row = Assert.Single(sheet.Expenses.Rows, r => r.CategoryId == food);
        Assert.Equal(270_900m, row.Actual);
        Assert.Equal(("USD", 10.5m), (row.CurrencyCode, row.OriginalActual));
        Assert.Equal(270_900m, sheet.Expenses.TotalActual);
    }

    private static MonthlyBudgetService Service(AppDbContext db) =>
        new(db, TestCurrencyConverter.Create(db, new FixedTimeProvider(Now)));

    private static async Task<int> FoodAsync(AppDbContext db, MonthlyBudgetService service)
    {
        await service.EnsureDefaultCategoriesAsync(UserId);
        return (await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food")).Id;
    }

    private static AppDbContext CreateContext(params ExchangeRate[] rates)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SheetCurrency_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.ExchangeRates.AddRange(rates);
        db.SaveChanges();
        return db;
    }

    private static ExchangeRate Rate(DateOnly date, decimal rate) => new()
    {
        BaseCurrency = "USD", QuoteCurrency = "VND", Date = date, Rate = rate, Source = "test"
    };
}
