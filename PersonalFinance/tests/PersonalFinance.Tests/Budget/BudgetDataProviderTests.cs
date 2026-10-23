using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Budget;

/// <summary>
/// Unit and integration tests for IBudgetDataSourceService, SheetBudgetMonthReader,
/// TrackerBudgetMonthReader, and BudgetDataProvider.
/// </summary>
public class BudgetDataProviderTests
{
    private const string UserId = "provider-user";
    private const string OtherUserId = "other-user";
    private static readonly int[] ExpectedMonthsOctoberAndNovember = [10, 11];
    private static readonly int[] ExpectedYearsDescending = [2026, 2025, 2024];
    private static readonly int[] ExpectedMonthsMarchMayNovember = [3, 5, 11];
    private static readonly int[] ExpectedYears2026Only = [2026];

    private static (AppDbContext Context, ConcurrencySimulationInterceptor Interceptor, string DbName)
        CreateContextWithInterceptor()
    {
        string dbName = $"BudgetPrefConcurrency_{Guid.NewGuid():N}";
        var interceptor = new ConcurrencySimulationInterceptor();

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        return (new AppDbContext(options), interceptor, dbName);
    }

    private static AppDbContext CreateSecondaryContext(string dbName)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetDataProvider_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static (BudgetDataProvider provider, BudgetDataSourceService sourceService, SheetBudgetMonthReader
        sheetReader, TrackerBudgetMonthReader trackerReader) CreateServices(AppDbContext db)
    {
        var sourceService = new BudgetDataSourceService(db);
        var sheetReader = new SheetBudgetMonthReader(db);
        var trackerReader = new TrackerBudgetMonthReader(db);
        var provider = new BudgetDataProvider(sourceService, sheetReader, trackerReader);
        return (provider, sourceService, sheetReader, trackerReader);
    }

    [Fact]
    public async Task DataSourcePreference_DefaultResolution_WhenNoSnapshots_ReturnsBudgetTracker()
    {
        await using AppDbContext db = CreateContext();
        (BudgetDataProvider _, BudgetDataSourceService? sourceService, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        BudgetDataSourceDto pref = await sourceService.GetDataSourcePreferenceAsync(UserId);

        Assert.Equal(BudgetDataSource.BudgetTracker, pref.Source);
        Assert.True(pref.IsDefault);
    }

    [Fact]
    public async Task DataSourcePreference_DefaultResolution_WhenSnapshotsExist_ReturnsGoogleSheet()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            ConnectionId = 1
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService? sourceService, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        BudgetDataSourceDto pref = await sourceService.GetDataSourcePreferenceAsync(UserId);

        Assert.Equal(BudgetDataSource.GoogleSheet, pref.Source);
        Assert.True(pref.IsDefault);
    }

    [Fact]
    public async Task DataSourcePreference_ExplicitPreference_OverridesDefault()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            ConnectionId = 1
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService? sourceService, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        BudgetDataSourceDto saved = await sourceService.SetDataSourcePreferenceAsync(UserId, new SetBudgetDataSourceRequest
        {
            Source = BudgetDataSource.BudgetTracker
        });

        Assert.Equal(BudgetDataSource.BudgetTracker, saved.Source);
        Assert.False(saved.IsDefault);

        BudgetDataSourceDto pref = await sourceService.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.BudgetTracker, pref.Source);
        Assert.False(pref.IsDefault);
    }

    [Fact]
    public async Task SheetBudgetMonthReader_GetMonth_MapsSnapshotDataCorrectly()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            MonthName = "October",
            BudgetedIncome = 5000m,
            ActualIncome = 5200m,
            BudgetedExpenses = 3000m,
            ActualExpenses = 2800m,
            StartingBalance = 1000m,
            EndingBalance = 3400m,
            CategoryBreakdownJson = "[{\"categoryName\":\"Food\",\"budgetedAmount\":500,\"actualAmount\":450}]"
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader _) = CreateServices(db);

        BudgetMonthData? month = await sheetReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Equal(2026, month.Year);
        Assert.Equal(10, month.Month);
        Assert.Equal("October", month.MonthName);
        Assert.Equal(5000m, month.BudgetedIncome);
        Assert.Equal(5200m, month.ActualIncome);
        Assert.Equal(3000m, month.BudgetedExpenses);
        Assert.Equal(2800m, month.ActualExpenses);
        Assert.Equal(1000m, month.StartingBalance);
        Assert.Equal(3400m, month.EndingBalance);
        Assert.Single(month.Categories);
        Assert.Equal("Food", month.Categories[0].Name);
        Assert.Equal(500m, month.Categories[0].Budgeted);
        Assert.Equal(450m, month.Categories[0].Actual);
    }

    [Fact]
    public async Task SheetBudgetMonthReader_GetTransactions_RespectsDateRangeAndTake()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                Id = 1,
                UserId = UserId,
                Date = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                Amount = 25m,
                Description = "Lunch",
                Category = "Food",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                Id = 2,
                UserId = UserId,
                Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Amount = 100m,
                Description = "Dinner",
                Category = "Food",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                Id = 3,
                UserId = UserId,
                Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                Amount = 50m,
                Description = "Groceries",
                Category = "Food",
                Type = "Expense"
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader _) = CreateServices(db);

        List<BudgetTransactionData> txs = await sheetReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(2, txs.Count);
        Assert.Equal(2, txs[0].Id); // Sorted by Date desc
        Assert.Equal(1, txs[1].Id);

        List<BudgetTransactionData> singleTx =
            await sheetReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
                take: 1);
        Assert.Single(singleTx);
        Assert.Equal(2, singleTx[0].Id);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_GetMonth_CalculatesActualFromTransactions()
    {
        await using AppDbContext db = CreateContext();
        var catFood = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        var catPay = new BudgetCategory
        { Id = 2, UserId = UserId, Name = "Paycheck", Kind = BudgetCategoryKind.Income, SortOrder = 10 };
        db.BudgetCategories.AddRange(catFood, catPay);

        var plan = new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 400m },
                new() { PlanId = 1, UserId = UserId, CategoryId = 2, PlannedAmount = 3000m }
            }
        };
        db.MonthlyBudgetPlans.Add(plan);

        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                Id = 1,
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 10, 5),
                Amount = 120m,
                Description = "Groceries"
            },
            new BudgetTransaction
            {
                Id = 2,
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 10, 10),
                Amount = 80m,
                Description = "Dining"
            },
            new BudgetTransaction
            {
                Id = 3,
                UserId = UserId,
                CategoryId = 2,
                Date = new DateOnly(2026, 10, 1),
                Amount = 3200m,
                Description = "Salary"
            },
            new BudgetTransaction
            {
                Id = 4,
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 11, 1),
                Amount = 50m,
                Description = "November spend"
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        BudgetMonthData? month = await trackerReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Equal(2026, month.Year);
        Assert.Equal(10, month.Month);
        Assert.Equal("October", month.MonthName);
        Assert.Equal(400m, month.BudgetedExpenses);
        Assert.Equal(200m, month.ActualExpenses); // 120 + 80
        Assert.Equal(3000m, month.BudgetedIncome);
        Assert.Equal(3200m, month.ActualIncome);

        BudgetMonthCategoryData foodCat = month.Categories.Single(c => c.Name == "Food");
        Assert.Equal(400m, foodCat.Budgeted);
        Assert.Equal(200m, foodCat.Actual);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_GetMonth_ActualOverrideReplacesTransactionSum()
    {
        await using AppDbContext db = CreateContext();
        var catFood = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        db.BudgetCategories.Add(catFood);

        var plan = new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 400m, ActualOverride = 350m }
            }
        };
        db.MonthlyBudgetPlans.Add(plan);

        // Transaction is 100m, but ActualOverride is 350m
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            Id = 1,
            UserId = UserId,
            CategoryId = 1,
            Date = new DateOnly(2026, 10, 5),
            Amount = 100m,
            Description = "Groceries"
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        BudgetMonthData? month = await trackerReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Equal(350m, month.ActualExpenses);
        BudgetMonthCategoryData foodCat = month.Categories.Single(c => c.Name == "Food");
        Assert.Equal(350m, foodCat.Actual);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_GetMonth_IncludesArchivedCategoryWithData_ExcludesEmptyArchived()
    {
        await using AppDbContext db = CreateContext();
        var catActive = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Active Cat", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        var catArchivedWithLine = new BudgetCategory
        {
            Id = 2,
            UserId = UserId,
            Name = "Archived With Line",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 20,
            IsDeleted = true
        };
        var catArchivedEmpty = new BudgetCategory
        {
            Id = 3,
            UserId = UserId,
            Name = "Archived Empty",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 30,
            IsDeleted = true
        };
        db.BudgetCategories.AddRange(catActive, catArchivedWithLine, catArchivedEmpty);

        var plan = new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 100m },
                new() { PlanId = 1, UserId = UserId, CategoryId = 2, PlannedAmount = 250m }
            }
        };
        db.MonthlyBudgetPlans.Add(plan);
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        BudgetMonthData? month = await trackerReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Equal(2, month.Categories.Count);
        Assert.Contains(month.Categories, c => c.Name == "Active Cat");
        Assert.Contains(month.Categories, c => c.Name == "Archived With Line");
        Assert.DoesNotContain(month.Categories, c => c.Name == "Archived Empty");
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_GetTransactions_RespectsDateRangeAndMapsType()
    {
        await using AppDbContext db = CreateContext();
        var catFood = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        var catPay = new BudgetCategory
        { Id = 2, UserId = UserId, Name = "Paycheck", Kind = BudgetCategoryKind.Income, SortOrder = 10 };
        db.BudgetCategories.AddRange(catFood, catPay);

        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                Id = 1,
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 10, 10),
                Amount = 50m,
                Description = "Lunch"
            },
            new BudgetTransaction
            {
                Id = 2,
                UserId = UserId,
                CategoryId = 2,
                Date = new DateOnly(2026, 10, 15),
                Amount = 2000m,
                Description = "Salary"
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        List<BudgetTransactionData> txs = await trackerReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31));

        Assert.Equal(2, txs.Count);
        Assert.Equal("Income", txs[0].Type);
        Assert.Equal("Expense", txs[1].Type);
    }

    [Fact]
    public async Task BudgetDataProvider_ActiveSource_RoutesToCorrectReader()
    {
        await using AppDbContext db = CreateContext();
        // Seed Google Sheet snapshot
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            BudgetedExpenses = 1111m,
            ActualExpenses = 2222m,
            ConnectionId = 1
        });

        // Seed Tracker plan
        var cat = new BudgetCategory { Id = 1, UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense };
        db.BudgetCategories.Add(cat);
        db.MonthlyBudgetPlans.Add(new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 3333m, ActualOverride = 4444m }
            }
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService? sourceService, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        // Default should be Google Sheet because snapshot exists
        BudgetMonthData? sheetMonth = await provider.GetMonthAsync(UserId, 2026, 10);
        Assert.NotNull(sheetMonth);
        Assert.Equal(1111m, sheetMonth.BudgetedExpenses);
        Assert.Equal(2222m, sheetMonth.ActualExpenses);

        // Switch to Budget Tracker
        await sourceService.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker });

        BudgetMonthData? trackerMonth = await provider.GetMonthAsync(UserId, 2026, 10);
        Assert.NotNull(trackerMonth);
        Assert.Equal(3333m, trackerMonth.BudgetedExpenses);
        Assert.Equal(4444m, trackerMonth.ActualExpenses);
    }

    [Fact]
    public async Task BudgetDataProvider_SheetTrend_PreservesEachSnapshotAndItsCount()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.AddRange(
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 1,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                ActualIncome = 3000m,
                ActualExpenses = 2000m
            },
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 2,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                ActualIncome = 2500m,
                ActualExpenses = 1500m
            },
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 1,
                Year = 2026,
                MonthNumber = 11,
                MonthName = "November",
                ActualIncome = 0m,
                ActualExpenses = 100m
            },
            new SpreadsheetBudgetSnapshot
            {
                UserId = OtherUserId,
                ConnectionId = 3,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                ActualIncome = 999m
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);
        BudgetDataProvider readPath = provider;
        List<BudgetMonthData> months = await readPath.GetMonthsAsync(UserId, 2026);
        BudgetMonthData? october = await readPath.GetMonthAsync(UserId, 2026, 10);
        List<BudgetTrendRecordData> records = await readPath.GetTrendRecordsAsync(UserId, 2026);

        Assert.NotNull(october);
        Assert.Equal(2, october.SourceRecordCount);
        Assert.Equal(2, months[0].SourceRecordCount);
        Assert.Equal(1, months[1].SourceRecordCount);
        Assert.Equal(3, months.Sum(m => m.SourceRecordCount));
        Assert.Collection(records,
            first =>
            {
                Assert.Equal(10, first.MonthNumber);
                Assert.Equal("October", first.MonthName);
                Assert.Equal(3000m, first.Income);
                Assert.Equal(2000m, first.Expenses);
                Assert.Equal(1000m, first.NetSavings);
                Assert.Equal(33.3m, first.SavingsRate);
            },
            second =>
            {
                Assert.Equal(10, second.MonthNumber);
                Assert.Equal(2500m, second.Income);
                Assert.Equal(1500m, second.Expenses);
                Assert.Equal(40.0m, second.SavingsRate);
            },
            third =>
            {
                Assert.Equal(11, third.MonthNumber);
                Assert.Equal(0, third.SavingsRate);
            });
        Assert.Empty(await readPath.GetTrendRecordsAsync(UserId, 2025));
        Assert.Single(await readPath.GetTrendRecordsAsync(OtherUserId, 2026));
    }

    [Fact]
    public async Task BudgetDataProvider_TrackerTrend_UsesOneEffectiveMonthPointAndNeverFallsBack()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 1,
            Year = 2026,
            MonthNumber = 9,
            MonthName = "September",
            ActualIncome = 999m
        });
        var expense = new BudgetCategory { UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense };
        var income = new BudgetCategory { UserId = UserId, Name = "Paycheck", Kind = BudgetCategoryKind.Income };
        db.BudgetCategories.AddRange(expense, income);
        await db.SaveChangesAsync();

        db.MonthlyBudgetPlans.Add(new MonthlyBudgetPlan
        {
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { UserId = UserId, CategoryId = expense.Id, PlannedAmount = 500m, ActualOverride = 250m },
                new() { UserId = UserId, CategoryId = income.Id, PlannedAmount = 3000m }
            }
        });
        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            { UserId = UserId, CategoryId = expense.Id, Date = new DateOnly(2026, 10, 5), Amount = 999m },
            new BudgetTransaction
            { UserId = UserId, CategoryId = income.Id, Date = new DateOnly(2026, 10, 5), Amount = 1000m },
            new BudgetTransaction
            { UserId = UserId, CategoryId = income.Id, Date = new DateOnly(2026, 11, 1), Amount = 500m });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService? sourceService, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);
        await sourceService.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker });

        BudgetDataProvider readPath = provider;
        List<BudgetMonthData> months = await readPath.GetMonthsAsync(UserId, 2026);
        List<BudgetTrendRecordData> records = await readPath.GetTrendRecordsAsync(UserId, 2026);

        Assert.Equal(ExpectedMonthsOctoberAndNovember, months.Select(m => m.Month));
        Assert.All(months, m => Assert.Equal(1, m.SourceRecordCount));
        Assert.Collection(records,
            october =>
            {
                Assert.Equal(10, october.MonthNumber);
                Assert.Equal("October", october.MonthName);
                Assert.Equal(1000m, october.Income);
                Assert.Equal(250m, october.Expenses);
                Assert.Equal(750m, october.NetSavings);
                Assert.Equal(75.0m, october.SavingsRate);
            },
            november =>
            {
                Assert.Equal(11, november.MonthNumber);
                Assert.Equal(500m, november.Income);
                Assert.Equal(0m, november.Expenses);
                Assert.Equal(100.0m, november.SavingsRate);
            });
        Assert.Empty(await readPath.GetTrendRecordsAsync(UserId, 2025));
    }

    [Fact]
    public async Task BudgetDataProvider_NoFallbackRule_WhenActiveSourceEmpty_ReturnsNullOrEmpty()
    {
        await using AppDbContext db = CreateContext();
        // Google Sheet has data for October 2026
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            BudgetedExpenses = 1000m,
            ConnectionId = 1
        });

        // Preference set explicitly to Budget Tracker (which has NO data for October 2026)
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        // Active source is BudgetTracker. Since it has no data, it MUST return null, never fall back to Google Sheet!
        BudgetMonthData? month = await provider.GetMonthAsync(UserId, 2026, 10);
        Assert.Null(month);

        List<BudgetMonthData> months = await provider.GetMonthsAsync(UserId, 2026);
        Assert.Empty(months);

        List<BudgetTrendRecordData> trend = await provider.GetTrendRecordsAsync(UserId, 2026);
        Assert.Empty(trend);

        List<BudgetTransactionData> txs = await provider.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Empty(txs);
    }

    [Fact]
    public async Task AvailableYears_AggregatesDistinctYearsFromPlansAndTransactions()
    {
        await using AppDbContext db = CreateContext();
        db.MonthlyBudgetPlans.AddRange(
            new MonthlyBudgetPlan { UserId = UserId, Year = 2025, Month = 12 },
            new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 1 });

        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            Date = new DateOnly(2024, 6, 15),
            Amount = 10m
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        List<int> years = await trackerReader.GetAvailableYearsAsync(UserId);

        Assert.Equal(ExpectedYearsDescending, years);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_GetMonthsAsync_ReturnsAllActiveMonthsSorted()
    {
        await using AppDbContext db = CreateContext();
        db.MonthlyBudgetPlans.AddRange(
            new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 11 },
            new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 3 });

        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            Date = new DateOnly(2026, 5, 20),
            Amount = 50m
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        List<BudgetMonthData> months = await trackerReader.GetMonthsAsync(UserId, 2026);

        Assert.Equal(3, months.Count);
        Assert.Equal(ExpectedMonthsMarchMayNovember, months.Select(m => m.Month));
    }

    [Fact]
    public async Task UserIsolation_OtherUsersDataNeverVisibleInReadersOrSourceService()
    {
        await using AppDbContext db = CreateContext();

        // Seed data for OtherUserId
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = OtherUserId,
            Year = 2026,
            MonthNumber = 10,
            BudgetedExpenses = 9999m,
            ActualExpenses = 8888m,
            ConnectionId = 1
        });

        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        {
            Id = 100,
            UserId = OtherUserId,
            Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            Amount = 500m,
            Description = "Other transaction",
            Category = "Other",
            Type = "Expense"
        });

        var otherCat = new BudgetCategory
        { Id = 10, UserId = OtherUserId, Name = "OtherCat", Kind = BudgetCategoryKind.Expense };
        db.BudgetCategories.Add(otherCat);
        db.MonthlyBudgetPlans.Add(new MonthlyBudgetPlan
        {
            Id = 10,
            UserId = OtherUserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 10, UserId = OtherUserId, CategoryId = 10, PlannedAmount = 7777m }
            }
        });
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            Id = 200,
            UserId = OtherUserId,
            CategoryId = 10,
            Date = new DateOnly(2026, 10, 20),
            Amount = 250m
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService? sourceService, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        // 1. Source preference default for UserId is BudgetTracker (since UserId has no snapshots)
        BudgetDataSourceDto pref = await sourceService.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.BudgetTracker, pref.Source);
        Assert.True(pref.IsDefault);

        // 2. Sheet reader returns null/empty for UserId
        BudgetMonthData? sheetMonth = await sheetReader.GetMonthAsync(UserId, 2026, 10);
        Assert.Null(sheetMonth);

        List<BudgetTransactionData> sheetTxs =
            await sheetReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Empty(sheetTxs);

        // 3. Tracker reader returns null/empty for UserId
        BudgetMonthData? trackerMonth = await trackerReader.GetMonthAsync(UserId, 2026, 10);
        Assert.Null(trackerMonth);

        List<BudgetTransactionData> trackerTxs =
            await trackerReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Empty(trackerTxs);

        // 4. Provider returns null for UserId
        BudgetMonthData? providerMonth = await provider.GetMonthAsync(UserId, 2026, 10);
        Assert.Null(providerMonth);
    }

    [Fact]
    public async Task DataSourcePreference_ConcurrentInsertRace_RecoversAndUpdatesExistingRow()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string? dbName) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new BudgetDataSourceService(db);

            interceptor.ShouldThrow = true;
            interceptor.OnSavingChangesAsync = async _ =>
            {
                await using AppDbContext secondaryDb = CreateSecondaryContext(dbName);
                var secondaryService = new BudgetDataSourceService(secondaryDb);
                await secondaryService.SetDataSourcePreferenceAsync(UserId, new SetBudgetDataSourceRequest
                {
                    Source = BudgetDataSource.GoogleSheet
                });
            };

            // Set preference to BudgetTracker. It should catch DbUpdateException, detach, reload, and update to BudgetTracker
            BudgetDataSourceDto result = await service.SetDataSourcePreferenceAsync(UserId, new SetBudgetDataSourceRequest
            {
                Source = BudgetDataSource.BudgetTracker
            });

            Assert.Equal(BudgetDataSource.BudgetTracker, result.Source);
            Assert.False(result.IsDefault);

            UserBudgetSourcePreference inDb = await db.UserBudgetSourcePreferences.SingleAsync(p => p.UserId == UserId);
            Assert.Equal(BudgetDataSource.BudgetTracker, inDb.Source);
        }
    }

    [Fact]
    public async Task DataSourcePreference_ConcurrentInsertRace_RethrowsWhenNoRowExists()
    {
        (AppDbContext? db, ConcurrencySimulationInterceptor? interceptor, string _) = CreateContextWithInterceptor();
        await using (db)
        {
            var service = new BudgetDataSourceService(db);

            interceptor.ShouldThrow = true; // Throws DbUpdateException without inserting anything into DB
            interceptor.OnSavingChangesAsync = _ => ValueTask.CompletedTask;

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.SetDataSourcePreferenceAsync(UserId, new SetBudgetDataSourceRequest
                {
                    Source = BudgetDataSource.BudgetTracker
                }));
        }
    }

    [Fact]
    public async Task BudgetDataProvider_DefaultSource_RoutesTransactionsAndAvailableYears()
    {
        await using AppDbContext db = CreateContext();
        // Seed Google Sheet snapshot and transaction
        var snapshot = new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            BudgetedExpenses = 1000m,
            ConnectionId = 1
        };
        db.SpreadsheetBudgetSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        {
            Id = 1,
            UserId = UserId,
            SnapshotId = snapshot.Id,
            Date = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc),
            Amount = 75m,
            Description = "Sheet Grocery",
            Category = "Food",
            Type = "Expense"
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader _) = CreateServices(db);

        // Default source is GoogleSheet because snapshot exists
        BudgetDataSource activeSource = await provider.GetActiveSourceAsync(UserId);
        Assert.Equal(BudgetDataSource.GoogleSheet, activeSource);

        List<BudgetTransactionData> txs = await provider.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        Assert.Single(txs);
        Assert.Equal("Sheet Grocery", txs[0].Description);

        List<int> years = await provider.GetAvailableYearsAsync(UserId);
        Assert.Equal(ExpectedYears2026Only, years);
    }

    [Fact]
    public async Task SheetBudgetMonthReader_MultiSnapshot_AggregatesTotalsAndMergesCategories()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.AddRange(
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 1,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                BudgetedIncome = 3000m,
                ActualIncome = 3100m,
                BudgetedExpenses = 2000m,
                ActualExpenses = 1900m,
                StartingBalance = 5000m,
                EndingBalance = 6200m,
                CategoryBreakdownJson = "[{\"categoryName\":\"Food\",\"budgetedAmount\":400,\"actualAmount\":350}]"
            },
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 2,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                BudgetedIncome = 1500m,
                ActualIncome = 1600m,
                BudgetedExpenses = 1000m,
                ActualExpenses = 900m,
                StartingBalance = 2000m,
                EndingBalance = 2700m,
                CategoryBreakdownJson =
                    "[{\"categoryName\":\"food\",\"budgetedAmount\":200,\"actualAmount\":150},{\"categoryName\":\"Rent\",\"budgetedAmount\":800,\"actualAmount\":800}]"
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader _) = CreateServices(db);

        // 1. GetMonthAsync aggregates both snapshots
        BudgetMonthData? month = await sheetReader.GetMonthAsync(UserId, 2026, 10);
        Assert.NotNull(month);
        Assert.Equal(4500m, month.BudgetedIncome); // 3000 + 1500
        Assert.Equal(4700m, month.ActualIncome); // 3100 + 1600
        Assert.Equal(3000m, month.BudgetedExpenses); // 2000 + 1000
        Assert.Equal(2800m, month.ActualExpenses); // 1900 + 900
        Assert.Equal(7000m, month.StartingBalance);
        Assert.Equal(8900m, month.EndingBalance);
        Assert.Equal(2, month.Categories.Count); // Food, Rent

        BudgetMonthCategoryData food = month.Categories.Single(c => c.Name.Equals("Food", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(600m, food.Budgeted); // 400 + 200
        Assert.Equal(500m, food.Actual); // 350 + 150

        // 2. GetMonthsAsync returns one aggregated month for month 10
        List<BudgetMonthData> months = await sheetReader.GetMonthsAsync(UserId, 2026);
        Assert.Single(months);
        Assert.Equal(10, months[0].Month);
        Assert.Equal(2800m, months[0].ActualExpenses);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_ZeroLineArchivedCategory_IsHidden()
    {
        await using AppDbContext db = CreateContext();

        var activeCat = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Active Rent", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        var archivedZeroCat = new BudgetCategory
        {
            Id = 2,
            UserId = UserId,
            Name = "Archived Zero",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 20,
            IsDeleted = true
        };
        var archivedWithDataCat = new BudgetCategory
        {
            Id = 3,
            UserId = UserId,
            Name = "Archived With Data",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 30,
            IsDeleted = true
        };

        db.BudgetCategories.AddRange(activeCat, archivedZeroCat, archivedWithDataCat);

        // Plan initialized when all 3 categories existed, so lines were created (archivedZeroCat has planned=0, no override)
        db.MonthlyBudgetPlans.Add(new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 1000m },
                new()
                {
                    PlanId = 1, UserId = UserId, CategoryId = 2, PlannedAmount = 0m
                }, // Zero line on archived category
                new()
                {
                    PlanId = 1, UserId = UserId, CategoryId = 3, PlannedAmount = 50m
                } // Non-zero line on archived category
            }
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        BudgetMonthData? month = await trackerReader.GetMonthAsync(UserId, 2026, 10);
        Assert.NotNull(month);

        // Only Active Rent and Archived With Data should be in categories list
        Assert.Equal(2, month.Categories.Count);
        Assert.Contains(month.Categories, c => c.Name == "Active Rent");
        Assert.Contains(month.Categories, c => c.Name == "Archived With Data");
        Assert.DoesNotContain(month.Categories, c => c.Name == "Archived Zero");
    }

    [Fact]
    public async Task TransactionDateBoundaries_FirstAndLastDayOfMonths()
    {
        await using AppDbContext db = CreateContext();
        var snapshot = new SpreadsheetBudgetSnapshot
        { UserId = UserId, Year = 2026, MonthNumber = 10, ConnectionId = 1 };
        db.SpreadsheetBudgetSnapshots.Add(snapshot);

        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                Id = 1,
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Amount = 10m,
                Description = "First Day",
                Category = "General",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                Id = 2,
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
                Amount = 20m,
                Description = "Last Day",
                Category = "General",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                Id = 3,
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                Amount = 30m,
                Description = "Next Month",
                Category = "General",
                Type = "Expense"
            });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader _) = CreateServices(db);

        List<BudgetTransactionData> txs = await sheetReader.GetTransactionsAsync(UserId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(2, txs.Count);
        Assert.Contains(txs, t => t.Description == "First Day");
        Assert.Contains(txs, t => t.Description == "Last Day");
        Assert.DoesNotContain(txs, t => t.Description == "Next Month");
    }

    [Fact]
    public async Task MonthlyBudgetService_And_TrackerBudgetMonthReader_ParityTest()
    {
        await using AppDbContext db = CreateContext();
        var monthlyBudgetService = new MonthlyBudgetService(db);
        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        await monthlyBudgetService.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        BudgetCategory home = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Home");
        BudgetCategory paycheck = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Paycheck");
        BudgetCategory bonus = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Bonus");

        // Custom archived category with data
        var archivedExpense = new BudgetCategory
        {
            UserId = UserId,
            Name = "Old Vacation",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 99,
            IsDeleted = true
        };
        db.BudgetCategories.Add(archivedExpense);
        await db.SaveChangesAsync();

        // Set planned amounts
        await monthlyBudgetService.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 500m });
        await monthlyBudgetService.SetPlannedAsync(UserId, 2026, 10, home.Id, new SetPlannedRequest { Amount = 1200m });
        await monthlyBudgetService.SetPlannedAsync(UserId, 2026, 10, paycheck.Id,
            new SetPlannedRequest { Amount = 4000m });
        await monthlyBudgetService.SetPlannedAsync(UserId, 2026, 10, bonus.Id,
            new SetPlannedRequest { Amount = 1000m });

        // Set actual override on Home (planned 1200, override 1150)
        await monthlyBudgetService.SetActualOverrideAsync(UserId, 2026, 10, home.Id,
            new SetActualOverrideRequest { Amount = 1150m });

        // Add transactions
        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = food.Id,
                Date = new DateOnly(2026, 10, 5),
                Amount = 230m,
                Description = "Groceries"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = food.Id,
                Date = new DateOnly(2026, 10, 12),
                Amount = 70m,
                Description = "Snacks"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = home.Id,
                Date = new DateOnly(2026, 10, 1),
                Amount = 999m,
                Description = "Ignored by override"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = paycheck.Id,
                Date = new DateOnly(2026, 10, 15),
                Amount = 4200m,
                Description = "Salary"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = archivedExpense.Id,
                Date = new DateOnly(2026, 10, 20),
                Amount = 150m,
                Description = "Historical trip"
            }
        );
        await db.SaveChangesAsync();

        // Fetch sheet and normalized month data
        MonthlyBudgetSheetDto sheet = await monthlyBudgetService.GetSheetAsync(UserId, 2026, 10);
        BudgetMonthData? monthData = await trackerReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(sheet);
        Assert.NotNull(monthData);

        // Verify top-level totals parity
        Assert.Equal(sheet.Expenses.TotalPlanned, monthData.BudgetedExpenses);
        Assert.Equal(sheet.Expenses.TotalActual, monthData.ActualExpenses);
        Assert.Equal(sheet.Income.TotalPlanned, monthData.BudgetedIncome);
        Assert.Equal(sheet.Income.TotalActual, monthData.ActualIncome);

        // Verify expense categories in normalized monthData match sheet expenses
        Assert.Equal(sheet.Expenses.Rows.Count, monthData.Categories.Count);
        foreach (SheetRowDto expenseRow in sheet.Expenses.Rows)
        {
            BudgetMonthCategoryData? matchingCat = monthData.Categories.SingleOrDefault(c => c.Name == expenseRow.Name);
            Assert.NotNull(matchingCat);
            Assert.Equal(expenseRow.Planned, matchingCat.Budgeted);
            Assert.Equal(expenseRow.Actual, matchingCat.Actual);
            Assert.Equal(BudgetCategoryKind.Expense, matchingCat.Kind);
        }

        // Verify all items in monthData.Categories are strictly Expense kind
        Assert.All(monthData.Categories, c => Assert.Equal(BudgetCategoryKind.Expense, c.Kind));
        Assert.DoesNotContain(monthData.Categories, c => c.Name == "Paycheck");
        Assert.DoesNotContain(monthData.Categories, c => c.Name == "Bonus");
        Assert.DoesNotContain(monthData.Categories, c => c.Name == "Savings");
        Assert.DoesNotContain(monthData.Categories, c => c.Name == "Interest");
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_SeparatesExpenseAndIncomeCategories_WithSameName()
    {
        await using AppDbContext db = CreateContext();

        var expenseOther = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Other", Kind = BudgetCategoryKind.Expense, SortOrder = 10 };
        var incomeOther = new BudgetCategory
        { Id = 2, UserId = UserId, Name = "Other", Kind = BudgetCategoryKind.Income, SortOrder = 10 };
        db.BudgetCategories.AddRange(expenseOther, incomeOther);

        db.MonthlyBudgetPlans.Add(new MonthlyBudgetPlan
        {
            Id = 1,
            UserId = UserId,
            Year = 2026,
            Month = 10,
            Lines = new List<MonthlyBudgetLine>
            {
                new() { PlanId = 1, UserId = UserId, CategoryId = 1, PlannedAmount = 100m },
                new() { PlanId = 1, UserId = UserId, CategoryId = 2, PlannedAmount = 500m }
            }
        });

        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 10, 5),
                Amount = 75m,
                Description = "Misc expense"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = 2,
                Date = new DateOnly(2026, 10, 15),
                Amount = 600m,
                Description = "Misc income"
            }
        );
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);
        BudgetMonthData? month = await trackerReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Equal(100m, month.BudgetedExpenses);
        Assert.Equal(75m, month.ActualExpenses);
        Assert.Equal(500m, month.BudgetedIncome);
        Assert.Equal(600m, month.ActualIncome);

        // Categories must only contain the expense "Other"
        Assert.Single(month.Categories);
        Assert.Equal("Other", month.Categories[0].Name);
        Assert.Equal(100m, month.Categories[0].Budgeted);
        Assert.Equal(75m, month.Categories[0].Actual);
        Assert.Equal(BudgetCategoryKind.Expense, month.Categories[0].Kind);
    }

    [Fact]
    public async Task GetTransactionsAsync_OpenRangeWithTake_ReturnsNewestRecords_BothReaders()
    {
        await using AppDbContext db = CreateContext();

        // 1. Sheet transactions
        var snapshot = new SpreadsheetBudgetSnapshot
        { UserId = UserId, Year = 2026, MonthNumber = 10, ConnectionId = 1 };
        db.SpreadsheetBudgetSnapshots.Add(snapshot);
        for (int i = 1; i <= 10; i++)
        {
            db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
            {
                Id = i,
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2026, 1, i, 0, 0, 0, DateTimeKind.Utc),
                Amount = i * 10m,
                Description = $"Sheet Tx {i}",
                Category = "General",
                Type = "Expense"
            });
        }

        // 2. Tracker transactions
        var category = new BudgetCategory
        { Id = 1, UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense, SortOrder = 1 };
        db.BudgetCategories.Add(category);
        for (int i = 1; i <= 10; i++)
        {
            db.BudgetTransactions.Add(new BudgetTransaction
            {
                Id = i,
                UserId = UserId,
                CategoryId = 1,
                Date = new DateOnly(2026, 2, i),
                Amount = i * 5m,
                Description = $"Tracker Tx {i}"
            });
        }

        await db.SaveChangesAsync();

        (BudgetDataProvider? provider, BudgetDataSourceService? sourceService, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        // Test Sheet reader with open range and take: 5
        List<BudgetTransactionData> sheetTxs = await sheetReader.GetTransactionsAsync(UserId, DateOnly.MinValue, DateOnly.MaxValue, take: 5);
        Assert.Equal(5, sheetTxs.Count);
        Assert.Equal("Sheet Tx 10", sheetTxs[0].Description);
        Assert.Equal("Sheet Tx 6", sheetTxs[4].Description);

        // Test Tracker reader with open range and take: 5
        List<BudgetTransactionData> trackerTxs =
            await trackerReader.GetTransactionsAsync(UserId, DateOnly.MinValue, DateOnly.MaxValue, take: 5);
        Assert.Equal(5, trackerTxs.Count);
        Assert.Equal("Tracker Tx 10", trackerTxs[0].Description);
        Assert.Equal("Tracker Tx 6", trackerTxs[4].Description);

        // Test Provider delegates open range with take
        await sourceService.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker });
        List<BudgetTransactionData> providerTxs = await provider.GetTransactionsAsync(UserId, DateOnly.MinValue, DateOnly.MaxValue, take: 3);
        Assert.Equal(3, providerTxs.Count);
        Assert.Equal("Tracker Tx 10", providerTxs[0].Description);
    }

    [Fact]
    public async Task SheetBudgetMonthReader_PreservesNullStartingAndEndingBalances_WhenAllNull()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            Year = 2026,
            MonthNumber = 10,
            ConnectionId = 1,
            BudgetedExpenses = 1000m,
            ActualExpenses = 900m,
            StartingBalance = null,
            EndingBalance = null
        });
        await db.SaveChangesAsync();

        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader? sheetReader, TrackerBudgetMonthReader _) = CreateServices(db);
        BudgetMonthData? month = await sheetReader.GetMonthAsync(UserId, 2026, 10);

        Assert.NotNull(month);
        Assert.Null(month.StartingBalance);
        Assert.Null(month.EndingBalance);
    }

    [Fact]
    public async Task TrackerBudgetMonthReader_YearBoundsGuard_ReturnsEmptyOrNull()
    {
        await using AppDbContext db = CreateContext();
        (BudgetDataProvider _, BudgetDataSourceService _, SheetBudgetMonthReader _, TrackerBudgetMonthReader? trackerReader) = CreateServices(db);

        List<BudgetMonthData> monthsInvalid = await trackerReader.GetMonthsAsync(UserId, 9999);
        Assert.Empty(monthsInvalid);

        BudgetMonthData? monthInvalid = await trackerReader.GetMonthAsync(UserId, 9999, 1);
        Assert.Null(monthInvalid);
    }

    private sealed class ConcurrencySimulationInterceptor : SaveChangesInterceptor
    {
        public Func<AppDbContext, ValueTask>? OnSavingChangesAsync { get; set; }
        public bool ShouldThrow { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (OnSavingChangesAsync != null && eventData.Context is AppDbContext context)
            {
                Func<AppDbContext, ValueTask> handler = OnSavingChangesAsync;
                OnSavingChangesAsync = null; // Execute only once for the target operation
                await handler(context);
                if (ShouldThrow)
                {
                    throw new DbUpdateException("Simulated concurrency conflict", (Exception?)null);
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
