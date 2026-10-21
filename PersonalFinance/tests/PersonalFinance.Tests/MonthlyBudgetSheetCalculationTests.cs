using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests;

/// <summary>
/// Unit tests verifying monthly budget sheet math, diff rules, totals, actual overrides (including 0 vs null), archived category visibility, and user isolation.
/// </summary>
public class MonthlyBudgetSheetCalculationTests
{
    private const string UserId = "user-sheet-math";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetSheet_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetSheet_SeedsDefaultCategories_WhenUserHasNone()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);

        var sheet = await service.GetSheetAsync(UserId, 2026, 10);

        Assert.NotNull(sheet);
        Assert.Equal(2026, sheet.Year);
        Assert.Equal(10, sheet.Month);
        Assert.Equal("October 2026", sheet.MonthName);
        Assert.NotEmpty(sheet.Expenses.Rows);
        Assert.NotEmpty(sheet.Income.Rows);
        Assert.Contains(sheet.Expenses.Rows, r => r.Name == "Food");
        Assert.Contains(sheet.Expenses.Rows, r => r.Name == "Utilities");
        Assert.Contains(sheet.Income.Rows, r => r.Name == "Paycheck");
        Assert.Contains(sheet.Income.Rows, r => r.Name == "Savings");
    }

    [Fact]
    public async Task GetSheet_ComputesActualAsSumOfTransactions()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        var paycheck = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Paycheck");

        // Set planned amounts
        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 500m });
        await service.SetPlannedAsync(UserId, 2026, 10, paycheck.Id, new SetPlannedRequest { Amount = 4000m });

        // Add transactions in October 2026
        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = food.Id,
                Date = new DateOnly(2026, 10, 5),
                Amount = 120.50m,
                Description = "Groceries"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = food.Id,
                Date = new DateOnly(2026, 10, 18),
                Amount = 79.50m,
                Description = "Dining out"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = paycheck.Id,
                Date = new DateOnly(2026, 10, 15),
                Amount = 4200m,
                Description = "Salary"
            }
        );
        await db.SaveChangesAsync();

        var sheet = await service.GetSheetAsync(UserId, 2026, 10);

        var foodRow = sheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
        Assert.Equal(500m, foodRow.Planned);
        Assert.Equal(200m, foodRow.Actual); // 120.50 + 79.50
        Assert.Equal(300m, foodRow.Diff); // 500 - 200 (positive = under budget)
        Assert.False(foodRow.ActualIsOverride);

        var paycheckRow = sheet.Income.Rows.Single(r => r.CategoryId == paycheck.Id);
        Assert.Equal(4000m, paycheckRow.Planned);
        Assert.Equal(4200m, paycheckRow.Actual);
        Assert.Equal(200m, paycheckRow.Diff); // 4200 - 4000 (positive = earned more)
        Assert.False(paycheckRow.ActualIsOverride);
    }

    [Fact]
    public async Task GetSheet_CalculatesTotalsAndDiffRulesCorrectly()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        var utilities = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Utilities");
        var paycheck = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Paycheck");
        var bonus = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Bonus");

        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 300m });
        await service.SetPlannedAsync(UserId, 2026, 10, utilities.Id, new SetPlannedRequest { Amount = 200m });
        await service.SetPlannedAsync(UserId, 2026, 10, paycheck.Id, new SetPlannedRequest { Amount = 3000m });
        await service.SetPlannedAsync(UserId, 2026, 10, bonus.Id, new SetPlannedRequest { Amount = 1000m });

        // Overspend on food: planned 300, actual 350 -> diff -50 (negative = over budget)
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = food.Id,
            Date = new DateOnly(2026, 10, 1),
            Amount = 350m,
            Description = "Costco"
        });
        // Underspend on utilities: planned 200, actual 150 -> diff +50 (positive = under budget)
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = utilities.Id,
            Date = new DateOnly(2026, 10, 10),
            Amount = 150m,
            Description = "Electric"
        });
        // Earned more on paycheck: planned 3000, actual 3200 -> diff +200 (positive = earned more than planned)
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = paycheck.Id,
            Date = new DateOnly(2026, 10, 15),
            Amount = 3200m,
            Description = "Salary"
        });
        // Earned less on bonus: planned 1000, actual 600 -> diff -400 (negative = earned less than planned)
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = bonus.Id,
            Date = new DateOnly(2026, 10, 20),
            Amount = 600m,
            Description = "Partial bonus"
        });
        await db.SaveChangesAsync();

        var sheet = await service.GetSheetAsync(UserId, 2026, 10);

        // Check individual expense row Diffs
        var foodRow = sheet.Expenses.Rows.Single(r => r.CategoryId == food.Id);
        Assert.Equal(300m, foodRow.Planned);
        Assert.Equal(350m, foodRow.Actual);
        Assert.Equal(-50m, foodRow.Diff); // 300 - 350 = -50 (negative: over budget)

        var utilitiesRow = sheet.Expenses.Rows.Single(r => r.CategoryId == utilities.Id);
        Assert.Equal(200m, utilitiesRow.Planned);
        Assert.Equal(150m, utilitiesRow.Actual);
        Assert.Equal(50m, utilitiesRow.Diff); // 200 - 150 = +50 (positive: under budget)

        // Expense section totals: Planned = 500, Actual = 500, Diff = 0
        Assert.Equal(500m, sheet.Expenses.TotalPlanned);
        Assert.Equal(500m, sheet.Expenses.TotalActual);
        Assert.Equal(0m, sheet.Expenses.TotalDiff);

        // Check individual income row Diffs
        var paycheckRow = sheet.Income.Rows.Single(r => r.CategoryId == paycheck.Id);
        Assert.Equal(3000m, paycheckRow.Planned);
        Assert.Equal(3200m, paycheckRow.Actual);
        Assert.Equal(200m, paycheckRow.Diff); // 3200 - 3000 = +200 (positive: earned more)

        var bonusRow = sheet.Income.Rows.Single(r => r.CategoryId == bonus.Id);
        Assert.Equal(1000m, bonusRow.Planned);
        Assert.Equal(600m, bonusRow.Actual);
        Assert.Equal(-400m, bonusRow.Diff); // 600 - 1000 = -400 (negative: earned less)

        // Income section totals: Planned = 4000, Actual = 3800, Diff = -200
        Assert.Equal(4000m, sheet.Income.TotalPlanned);
        Assert.Equal(3800m, sheet.Income.TotalActual);
        Assert.Equal(-200m, sheet.Income.TotalDiff);
    }

    [Fact]
    public async Task ActualOverride_ReplacesTransactionSum_AndCanBeReverted()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 400m });

        // Transaction sum is 100
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = food.Id,
            Date = new DateOnly(2026, 10, 5),
            Amount = 100m,
            Description = "Market"
        });
        await db.SaveChangesAsync();

        // 1. Set manual override to 250
        var sheetWithOverride = await service.SetActualOverrideAsync(UserId, 2026, 10, food.Id,
            new SetActualOverrideRequest { Amount = 250m });
        var foodRowOverride = sheetWithOverride.Expenses.Rows.Single(r => r.CategoryId == food.Id);

        Assert.Equal(250m, foodRowOverride.Actual);
        Assert.True(foodRowOverride.ActualIsOverride);
        Assert.Equal(150m, foodRowOverride.Diff); // 400 - 250

        // 2. Revert override (pass null)
        var sheetReverted = await service.SetActualOverrideAsync(UserId, 2026, 10, food.Id,
            new SetActualOverrideRequest { Amount = null });
        var foodRowReverted = sheetReverted.Expenses.Rows.Single(r => r.CategoryId == food.Id);

        Assert.Equal(100m, foodRowReverted.Actual);
        Assert.False(foodRowReverted.ActualIsOverride);
        Assert.Equal(300m, foodRowReverted.Diff); // 400 - 100
    }

    [Fact]
    public async Task ActualOverride_ZeroOverride_SetsActualToZeroUnlikeNoOverride()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 200m });

        // Transactions totaling 150
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = food.Id,
            Date = new DateOnly(2026, 10, 12),
            Amount = 150m,
            Description = "Restaurant"
        });
        await db.SaveChangesAsync();

        // Explicit override of 0.00
        var sheetWithZero =
            await service.SetActualOverrideAsync(UserId, 2026, 10, food.Id,
                new SetActualOverrideRequest { Amount = 0m });
        var foodRowZero = sheetWithZero.Expenses.Rows.Single(r => r.CategoryId == food.Id);

        Assert.Equal(0m, foodRowZero.Actual);
        Assert.True(foodRowZero.ActualIsOverride);
        Assert.Equal(200m, foodRowZero.Diff); // 200 - 0 = 200
    }

    [Fact]
    public async Task ArchivedCategory_AppearsInMonthsWithData_AndIsHiddenFromEmptyMonths()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        // Add custom category and record data in October 2026
        var customCat = await service.CreateCategoryAsync(UserId,
            new SaveCategoryRequest { Name = "Old Subscription", Kind = BudgetCategoryKind.Expense, SortOrder = 50 });
        await service.SetPlannedAsync(UserId, 2026, 10, customCat.Id, new SetPlannedRequest { Amount = 50m });

        // Now archive the category
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // October 2026 has planned amount for Old Subscription -> MUST appear with IsArchived == true
        var octSheet = await service.GetSheetAsync(UserId, 2026, 10);
        var octRow = octSheet.Expenses.Rows.FirstOrDefault(r => r.CategoryId == customCat.Id);
        Assert.NotNull(octRow);
        Assert.Equal("Old Subscription", octRow.Name);
        Assert.True(octRow.IsArchived);
        Assert.Equal(50m, octRow.Planned);

        // November 2026 has no data for Old Subscription -> MUST NOT appear
        var novSheet = await service.GetSheetAsync(UserId, 2026, 11);
        var novRow = novSheet.Expenses.Rows.FirstOrDefault(r => r.CategoryId == customCat.Id);
        Assert.Null(novRow);
    }

    [Fact]
    public async Task ArchivedCategory_AppearsWhenItHasTransactionsOnly_EvenWithoutPlanned()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId,
            new SaveCategoryRequest { Name = "Archived Gym", Kind = BudgetCategoryKind.Expense, SortOrder = 50 });

        // Add transaction before archiving
        await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            CategoryId = customCat.Id,
            Date = new DateOnly(2026, 10, 10),
            Amount = 85m,
            Description = "Gym Annual Fee"
        });

        // Archive category
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // Sheet in October should display Archived Gym because of the transaction
        var octSheet = await service.GetSheetAsync(UserId, 2026, 10);
        var gymRow = octSheet.Expenses.Rows.FirstOrDefault(r => r.CategoryId == customCat.Id);

        Assert.NotNull(gymRow);
        Assert.Equal("Archived Gym", gymRow.Name);
        Assert.True(gymRow.IsArchived);
        Assert.Equal(0m, gymRow.Planned);
        Assert.Equal(85m, gymRow.Actual);
    }

    [Fact]
    public async Task UserIsolation_OtherUsersDataDoesNotAffectSheet()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);
        await service.EnsureDefaultCategoriesAsync(OtherUserId);

        var myFood = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        var otherFood = await db.BudgetCategories.FirstAsync(c => c.UserId == OtherUserId && c.Name == "Food");

        await service.SetPlannedAsync(UserId, 2026, 10, myFood.Id, new SetPlannedRequest { Amount = 300m });
        await service.SetPlannedAsync(OtherUserId, 2026, 10, otherFood.Id, new SetPlannedRequest { Amount = 1000m });

        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = OtherUserId,
            CategoryId = otherFood.Id,
            Date = new DateOnly(2026, 10, 15),
            Amount = 500m,
            Description = "Other user grocery"
        });
        await db.SaveChangesAsync();

        var mySheet = await service.GetSheetAsync(UserId, 2026, 10);
        var myFoodRow = mySheet.Expenses.Rows.Single(r => r.CategoryId == myFood.Id);

        Assert.Equal(300m, myFoodRow.Planned);
        Assert.Equal(0m, myFoodRow.Actual);
        Assert.Empty(mySheet.Transactions);
    }

    [Fact]
    public async Task SetPlanned_And_SetActualOverride_WithOtherUserCategory_ThrowsKeyNotFoundException()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);
        await service.EnsureDefaultCategoriesAsync(OtherUserId);

        var otherFood = await db.BudgetCategories.FirstAsync(c => c.UserId == OtherUserId && c.Name == "Food");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetPlannedAsync(UserId, 2026, 10, otherFood.Id, new SetPlannedRequest { Amount = 100m }));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetActualOverrideAsync(UserId, 2026, 10, otherFood.Id,
                new SetActualOverrideRequest { Amount = 100m }));
    }

    [Fact]
    public async Task SetPlanned_OnArchivedCategory_RejectsInFreshMonth_AllowsInHistoricalMonthWithData()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Archived Streaming",
            Kind = BudgetCategoryKind.Expense,
            SortOrder = 80
        });

        // Set planned amount in historical month (October 2026)
        await service.SetPlannedAsync(UserId, 2026, 10, customCat.Id, new SetPlannedRequest { Amount = 20m });

        // Archive the category
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // 1. Attempting to set planned amount or override in fresh month (December 2026) where it has no data -> KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetPlannedAsync(UserId, 2026, 12, customCat.Id, new SetPlannedRequest { Amount = 25m }));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetActualOverrideAsync(UserId, 2026, 12, customCat.Id,
                new SetActualOverrideRequest { Amount = 25m }));

        // A rejected edit must not create a December plan (new-month prompt must remain available)
        var decSheet = await service.GetSheetAsync(UserId, 2026, 12);
        Assert.False(decSheet.IsInitialized);
        Assert.True(decSheet.CanCopyFromPrevious);
        Assert.Equal(0,
            await db.MonthlyBudgetPlans.CountAsync(p => p.UserId == UserId && p.Year == 2026 && p.Month == 12));

        // 2. Editing in historical month (October 2026) where it already has data -> succeeds and updates history
        var updatedOct =
            await service.SetPlannedAsync(UserId, 2026, 10, customCat.Id, new SetPlannedRequest { Amount = 30m });
        var row = updatedOct.Expenses.Rows.Single(r => r.CategoryId == customCat.Id);
        Assert.Equal(30m, row.Planned);
        Assert.True(row.IsArchived);
    }

    [Fact]
    public async Task Updates_SetUpdatedAt_OnLinesAndPreference()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 100m });
        var line = await db.MonthlyBudgetLines.SingleAsync(l => l.UserId == UserId && l.CategoryId == food.Id);
        Assert.Null(line.UpdatedAt);

        await service.SetPlannedAsync(UserId, 2026, 10, food.Id, new SetPlannedRequest { Amount = 150m });
        Assert.NotNull(line.UpdatedAt);
        var plannedUpdatedAt = line.UpdatedAt;

        await service.SetActualOverrideAsync(UserId, 2026, 10, food.Id, new SetActualOverrideRequest { Amount = 80m });
        Assert.NotNull(line.UpdatedAt);
        Assert.True(line.UpdatedAt >= plannedUpdatedAt);

        var sourceService = new BudgetDataSourceService(db);
        await sourceService.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.GoogleSheet });
        var preference = await db.UserBudgetSourcePreferences.SingleAsync(p => p.UserId == UserId);
        Assert.Null(preference.UpdatedAt);

        await sourceService.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker });
        Assert.NotNull(preference.UpdatedAt);
        Assert.Equal(BudgetDataSource.BudgetTracker, preference.Source);
    }
}
