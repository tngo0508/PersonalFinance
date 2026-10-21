using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests;

/// <summary>
/// Unit tests for category lifecycle: idempotent default seeding, case-insensitive uniqueness, soft deletes, rename, kind immutability, and user isolation.
/// </summary>
public class BudgetCategoryTests
{
    private const string UserId = "user-categories";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetCategories_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureDefaultCategories_IsIdempotent_AndCreatesAllExpectedDefaults()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);

        // Run once
        await service.EnsureDefaultCategoriesAsync(UserId);
        var categories1 = await service.GetCategoriesAsync(UserId);

        Assert.Equal(16, categories1.Count); // 11 expenses + 5 income
        var expenseCategories = categories1.Where(c => c.Kind == BudgetCategoryKind.Expense).ToList();
        var incomeCategories = categories1.Where(c => c.Kind == BudgetCategoryKind.Income).ToList();

        Assert.Equal(11, expenseCategories.Count);
        Assert.Equal(5, incomeCategories.Count);

        // Run second time: should not duplicate
        await service.EnsureDefaultCategoriesAsync(UserId);
        var categories2 = await service.GetCategoriesAsync(UserId);
        Assert.Equal(16, categories2.Count);
    }

    [Fact]
    public async Task CreateCategory_RejectsDuplicateName_CaseInsensitivelyWithinKind()
    {
        await using var db = CreateContext();
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
        var incomeFood = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
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
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Freelance",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 10
        });

        var updated = await service.UpdateCategoryAsync(UserId, customCat.Id, new SaveCategoryRequest
        {
            Name = "Contract Work",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 5
        });

        Assert.Equal("Contract Work", updated.Name);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal(BudgetCategoryKind.Income, updated.Kind);

        var stored = await db.BudgetCategories.SingleAsync(c => c.Id == customCat.Id);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCategory_RejectsKindChange()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
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
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
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
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
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
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        var customCat = await service.CreateCategoryAsync(UserId, new SaveCategoryRequest
        {
            Name = "Freelance",
            Kind = BudgetCategoryKind.Income,
            SortOrder = 100
        });

        // Verify it is in active list
        var activeBefore = await service.GetCategoriesAsync(UserId, includeArchived: false);
        Assert.Contains(activeBefore, c => c.Id == customCat.Id);

        // Delete it
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // Verify it is excluded from active list
        var activeAfter = await service.GetCategoriesAsync(UserId, includeArchived: false);
        Assert.DoesNotContain(activeAfter, c => c.Id == customCat.Id);

        // Verify it is included when includeArchived = true
        var allCategories = await service.GetCategoriesAsync(UserId, includeArchived: true);
        var archived = allCategories.Single(c => c.Id == customCat.Id);
        Assert.True(archived.IsArchived);

        var stored = await db.BudgetCategories.IgnoreQueryFilters().SingleAsync(c => c.Id == customCat.Id);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task Category_OwnershipIsolation_PreventsCrossUserMutation()
    {
        await using var db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);
        await service.EnsureDefaultCategoriesAsync(OtherUserId);

        var myCategories = await service.GetCategoriesAsync(UserId);
        var myFirstCategory = myCategories[0];

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
}
