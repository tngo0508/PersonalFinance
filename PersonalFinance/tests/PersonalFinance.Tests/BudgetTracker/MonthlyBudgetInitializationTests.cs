using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// Unit tests for monthly plan initialization: blank init, copy from previous, nearest earlier detection across year/skipped months, archived category exclusion, and idempotency.
/// </summary>
public class MonthlyBudgetInitializationTests
{
    private const string UserId = "user-init";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetInit_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CanCopyFromPrevious_IsFalse_WhenNoEarlierMonthHasPlan()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));

        MonthlyBudgetSheetDto sheet = await service.GetSheetAsync(UserId, 2026, 10);

        Assert.False(sheet.IsInitialized);
        Assert.False(sheet.CanCopyFromPrevious);
        Assert.Null(sheet.PreviousPlanYear);
        Assert.Null(sheet.PreviousPlanMonth);
    }

    [Fact]
    public async Task CanCopyFromPrevious_IsTrue_WhenEarlierMonthHasPlan()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2026, 9, food.Id, new SetPlannedRequest { Amount = 600m });

        // Uninitialized October 2026 sheet should indicate September 2026 is available to copy
        MonthlyBudgetSheetDto octSheet = await service.GetSheetAsync(UserId, 2026, 10);

        Assert.False(octSheet.IsInitialized);
        Assert.True(octSheet.CanCopyFromPrevious);
        Assert.Equal(2026, octSheet.PreviousPlanYear);
        Assert.Equal(9, octSheet.PreviousPlanMonth);
    }

    [Fact]
    public async Task PreviousPlanMonth_IsGivenAsNumbers_SoThePageCanNameItInTheUserLanguage()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2025, 11, food.Id, new SetPlannedRequest { Amount = 600m });

        MonthlyBudgetSheetDto febSheet = await service.GetSheetAsync(UserId, 2026, 2);
        MonthlyBudgetSheetDto novSheet = await service.GetSheetAsync(UserId, 2025, 11);

        Assert.Equal(2025, febSheet.PreviousPlanYear);
        Assert.Equal(11, febSheet.PreviousPlanMonth);
        Assert.Null(novSheet.PreviousPlanYear);
        Assert.Null(novSheet.PreviousPlanMonth);
    }

    [Fact]
    public async Task InitializeMonth_CopyFromPrevious_CopiesOnlyPlannedAmounts()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        BudgetCategory paycheck = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Paycheck");

        // Set planned amounts, actual overrides, and transactions in September 2026
        await service.SetPlannedAsync(UserId, 2026, 9, food.Id, new SetPlannedRequest { Amount = 550m });
        await service.SetPlannedAsync(UserId, 2026, 9, paycheck.Id, new SetPlannedRequest { Amount = 4500m });
        await service.SetActualOverrideAsync(UserId, 2026, 9, food.Id,
            new SetActualOverrideRequest { Amount = 300m }); // override

        // Initialize October 2026 copying from September
        MonthlyBudgetSheetDto octSheet = await service.InitializeMonthAsync(UserId, new InitializeMonthRequest
        {
            Year = 2026,
            Month = 10,
            CopyFromPrevious = true
        });

        Assert.True(octSheet.IsInitialized);

        SheetRowDto foodRow = octSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
        Assert.Equal(550m, foodRow.Planned); // Planned copied
        Assert.Equal(0m, foodRow.Actual); // Actual is NOT copied
        Assert.False(foodRow.ActualIsOverride); // Override is NOT copied

        SheetRowDto paycheckRow = octSheet.Income.Rows.Single(r => r.CategoryId == paycheck.Id);
        Assert.Equal(4500m, paycheckRow.Planned);
        Assert.Equal(0m, paycheckRow.Actual);
    }

    [Fact]
    public async Task InitializeMonth_CopyFromPrevious_SkipsArchivedCategories()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Old Hobby",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 50
        });

        // Set planned amount in September 2026
        await service.SetPlannedAsync(UserId, 2026, 9, customCat.Id, new SetPlannedRequest { Amount = 120m });

        // Archive category before October
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // Initialize October 2026 copying from September
        MonthlyBudgetSheetDto octSheet = await service.InitializeMonthAsync(UserId, new InitializeMonthRequest
        {
            Year = 2026,
            Month = 10,
            CopyFromPrevious = true
        });

        // Archived category must NOT be copied or visible in October
        Assert.Null(octSheet.Expenses.Rows.FirstOrDefault(r => r.CategoryId == customCat.Id));
    }

    [Fact]
    public async Task InitializeMonth_CopyFromPrevious_TakesNearestEarlierMonthWhenMonthsSkippedOrYearChanges()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

        // Seed two earlier plans with different amounts: October 2025 (500) and November 2025 (750)
        await service.SetPlannedAsync(UserId, 2025, 10, food.Id, new SetPlannedRequest { Amount = 500m });
        await service.SetPlannedAsync(UserId, 2025, 11, food.Id, new SetPlannedRequest { Amount = 750m });

        // Also seed a future plan (March 2026) that must be ignored
        await service.SetPlannedAsync(UserId, 2026, 3, food.Id, new SetPlannedRequest { Amount = 1200m });

        // Uninitialized February 2026 sheet should show November 2025 as the nearest earlier month
        MonthlyBudgetSheetDto febBefore = await service.GetSheetAsync(UserId, 2026, 2);
        Assert.False(febBefore.IsInitialized);
        Assert.True(febBefore.CanCopyFromPrevious);
        Assert.Equal(2025, febBefore.PreviousPlanYear);
        Assert.Equal(11, febBefore.PreviousPlanMonth);

        // Initialize February 2026 copying from previous
        MonthlyBudgetSheetDto febSheet = await service.InitializeMonthAsync(UserId, new InitializeMonthRequest
        {
            Year = 2026,
            Month = 2,
            CopyFromPrevious = true
        });

        Assert.True(febSheet.IsInitialized);
        SheetRowDto foodRow = febSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
        Assert.Equal(750m,
            foodRow.Planned); // Must copy 750 from November 2025 (not 500 from Oct 2025, not 1200 from Mar 2026)
    }

    [Fact]
    public async Task InitializeMonth_StartBlank_LeavesPlannedAmountsAtZero()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2026, 9, food.Id, new SetPlannedRequest { Amount = 550m });

        // Initialize October 2026 with CopyFromPrevious = false
        MonthlyBudgetSheetDto octSheet = await service.InitializeMonthAsync(UserId, new InitializeMonthRequest
        {
            Year = 2026,
            Month = 10,
            CopyFromPrevious = false
        });

        Assert.True(octSheet.IsInitialized);
        Assert.All(octSheet.Expenses.Rows, r => Assert.Equal(0m, r.Planned));
        Assert.All(octSheet.Income.Rows, r => Assert.Equal(0m, r.Planned));
    }

    [Fact]
    public async Task InitializeMonth_IsIdempotent_DoubleSubmitDoesNotThrowOrDuplicate()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db, TestCurrencyConverter.Create(db));
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2026, 9, food.Id, new SetPlannedRequest { Amount = 600m });
        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 350m });

        var req = new InitializeMonthRequest
        {
            Year = 2026,
            Month = 10,
            CopyFromPrevious = true
        };

        MonthlyBudgetSheetDto sheet1 = await service.InitializeMonthAsync(UserId, req);
        MonthlyBudgetSheetDto sheet2 = await service.InitializeMonthAsync(UserId, req);

        Assert.True(sheet1.IsInitialized);
        Assert.True(sheet2.IsInitialized);
        Assert.Equal(sheet1.Expenses.Rows.Count, sheet2.Expenses.Rows.Count);

        // Ensure exactly one plan row exists in database for this month
        int planCount =
            await db.MonthlyBudgetPlans.CountAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 10);
        Assert.Equal(1, planCount);

        // Ensure second initialization does not overwrite existing planned amount
        SheetRowDto foodRow = sheet2.Expenses.Rows.Single(r => r.CategoryId == food.Id);
        Assert.Equal(350m, foodRow.Planned);
    }
}
