using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Budget;

/// <summary>
/// Unit tests for category lifecycle: idempotent default seeding, case-insensitive uniqueness, soft deletes, rename, kind immutability, and user isolation.
/// </summary>
public class BudgetCategoryTests
{
    private const string UserId = "user-categories";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetCategories_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureDefaultCategories_IsIdempotent_AndCreatesAllExpectedDefaults()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);

        // Run once
        await service.EnsureDefaultCategoriesAsync(UserId);
        List<BudgetCategoryDto> categories1 = await service.GetCategoriesAsync(UserId);

        Assert.Equal(16, categories1.Count); // 11 expenses + 5 income
        var expenseCategories = categories1.Where(c => c.Kind == BudgetCategoryKind.Expense).ToList();
        var incomeCategories = categories1.Where(c => c.Kind == BudgetCategoryKind.Income).ToList();

        Assert.Equal(11, expenseCategories.Count);
        Assert.Equal(5, incomeCategories.Count);

        // Run second time: should not duplicate
        await service.EnsureDefaultCategoriesAsync(UserId);
        List<BudgetCategoryDto> categories2 = await service.GetCategoriesAsync(UserId);
        Assert.Equal(16, categories2.Count);
    }

    [Fact]
    public async Task CreateCategory_RejectsDuplicateName_CaseInsensitivelyWithinKind()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        // "Food" already exists in Expense
        await Assert.ThrowsAsync<DuplicateCategoryException>(() =>
            service.CreateCategoryAsync(UserId, new SaveCategoryRequest
            {
                Name = "food",
                Kind = BudgetCategoryKind.Expense,
                SortOrder = 1
            }));

        await Assert.ThrowsAsync<DuplicateCategoryException>(() =>
            service.CreateCategoryAsync(UserId, new SaveCategoryRequest
            {
                Name = "FOOD",
                Kind = BudgetCategoryKind.Expense,
                SortOrder = 1
            }));

        // But "Food" in Income is allowed because kind is different
        BudgetCategoryDto incomeFood = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Food",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 1
        });
        Assert.NotNull(incomeFood);
    }

    [Fact]
    public async Task UpdateCategory_AllowsRenameAndReorder()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Freelance",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 10
        });

        BudgetCategoryDto updated = await service.UpdateCategoryAsync(UserId, customCat.Id, new SaveCategoryRequest
        {
            Name = "Contract Work",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 5
        });

        Assert.Equal("Contract Work", updated.Name);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal(BudgetCategoryKind.Income, updated.Kind);

        BudgetCategory stored = await db.BudgetCategories.SingleAsync(c => c.Id == customCat.Id);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCategory_RejectsKindChange()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Side Business",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 10
        });

        // Attempting to change kind from Income to Expense must throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateCategoryAsync(UserId, customCat.Id, new SaveCategoryRequest
            {
                Name = "Side Business",
                Kind = BudgetCategoryKind.Expense,
                SortOrder = 10
            }));
    }

    [Fact]
    public async Task UpdateCategory_RejectsDuplicateName()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Side Business",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 10
        });

        // "Paycheck" already exists in Income
        await Assert.ThrowsAsync<DuplicateCategoryException>(() =>
            service.UpdateCategoryAsync(UserId, customCat.Id, new SaveCategoryRequest
            {
                Name = "paycheck",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 10
            }));
    }

    [Fact]
    public async Task UpdateCategory_ArchivedOrNotFound_ThrowsKeyNotFoundException()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Side Business",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 10
        });

        await service.DeleteCategoryAsync(UserId, customCat.Id); // archive

        // Updating archived category throws KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateCategoryAsync(UserId, customCat.Id, new SaveCategoryRequest
            {
                Name = "Renamed",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 10
            }));

        // Updating non-existent category throws KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateCategoryAsync(UserId, 99999, new SaveCategoryRequest
            {
                Name = "NonExistent",
                Kind = BudgetCategoryKind.Income,
                SortOrder = 1
            }));
    }

    [Fact]
    public async Task DeleteCategory_SoftDeletesCategory_HidingItFromActiveList()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Freelance",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 100
        });

        // Verify it is in active list
        List<BudgetCategoryDto> activeBefore = await service.GetCategoriesAsync(UserId, includeArchived: false);
        Assert.Contains(activeBefore, c => c.Id == customCat.Id);

        // Delete it
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // Verify it is excluded from active list
        List<BudgetCategoryDto> activeAfter = await service.GetCategoriesAsync(UserId, includeArchived: false);
        Assert.DoesNotContain(activeAfter, c => c.Id == customCat.Id);

        // Verify it is included when includeArchived = true
        List<BudgetCategoryDto> allCategories = await service.GetCategoriesAsync(UserId, includeArchived: true);
        BudgetCategoryDto archived = allCategories.Single(c => c.Id == customCat.Id);
        Assert.True(archived.IsArchived);

        BudgetCategory stored = await db.BudgetCategories.IgnoreQueryFilters().SingleAsync(c => c.Id == customCat.Id);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task Category_OwnershipIsolation_PreventsCrossUserMutation()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);
        await service.EnsureDefaultCategoriesAsync(OtherUserId);

        List<BudgetCategoryDto> myCategories = await service.GetCategoriesAsync(UserId);
        BudgetCategoryDto myFirstCategory = myCategories[0];

        // Other user cannot update or delete my category
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateCategoryAsync(OtherUserId, myFirstCategory.Id, new SaveCategoryRequest
            {
                Name = "Renamed",
                Kind = myFirstCategory.Kind,
                SortOrder = 1
            }));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteCategoryAsync(OtherUserId, myFirstCategory.Id));
    }

    [Fact]
    public void DuplicateNameCheck_TranslatesToAzureSql_ComparingLoweredNames()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        IQueryable<BudgetCategory> query = db.BudgetCategories
            .Where(c => c.UserId == UserId && c.Kind == BudgetCategoryKind.Expense)
            .Where(MonthlyBudgetService.NameEqualsIgnoreCase("Groceries"));

        string sql = AzureSqlTranslation.AssertTranslates(query);

        // EF lowers the argument before sending it as a parameter; SQL lowers the column.
        Assert.Contains("LOWER([b].[Name]) = @", sql, StringComparison.Ordinal);
        Assert.Contains("N'groceries'", sql, StringComparison.Ordinal);
    }
}
