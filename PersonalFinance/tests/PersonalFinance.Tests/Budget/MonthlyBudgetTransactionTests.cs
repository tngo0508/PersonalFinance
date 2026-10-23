using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Budget;

/// <summary>
/// Unit tests for Budget Tracker transactions: date-based month membership, boundary partitions, CRUD, and category constraints.
/// </summary>
public class MonthlyBudgetTransactionTests
{
    private const string UserId = "user-transactions";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BudgetTransactions_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Transactions_ArePartitionedStrictlyByCalendarMonth()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

        // 1. Transaction on last day of October 2026 (2026-10-31)
        BudgetTransactionDto txOctEnd = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 31),
            Amount = 45m,
            Description = "Halloween treats",
            CategoryId = food.Id
        });

        // 2. Transaction on first day of November 2026 (2026-11-01)
        BudgetTransactionDto txNovStart = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 11, 1),
            Amount = 60m,
            Description = "November coffee",
            CategoryId = food.Id
        });

        // 3. Transaction on December 31, 2026 (2026-12-31)
        BudgetTransactionDto txDecEnd = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 12, 31),
            Amount = 100m,
            Description = "New Year's Eve dinner",
            CategoryId = food.Id
        });

        // 4. Transaction on January 1, 2027 (2027-01-01)
        BudgetTransactionDto txJanStart = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2027, 1, 1),
            Amount = 25m,
            Description = "New Year brunch",
            CategoryId = food.Id
        });

        // Verify October 2026
        MonthlyBudgetSheetDto octSheet = await service.GetSheetAsync(UserId, 2026, 10);
        Assert.Single(octSheet.Transactions);
        Assert.Equal(txOctEnd.Id, octSheet.Transactions[0].Id);
        Assert.Equal(45m, octSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);

        // Verify November 2026
        MonthlyBudgetSheetDto novSheet = await service.GetSheetAsync(UserId, 2026, 11);
        Assert.Single(novSheet.Transactions);
        Assert.Equal(txNovStart.Id, novSheet.Transactions[0].Id);
        Assert.Equal(60m, novSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);

        // Verify December 2026
        MonthlyBudgetSheetDto decSheet = await service.GetSheetAsync(UserId, 2026, 12);
        Assert.Single(decSheet.Transactions);
        Assert.Equal(txDecEnd.Id, decSheet.Transactions[0].Id);
        Assert.Equal(100m, decSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);

        // Verify January 2027
        MonthlyBudgetSheetDto janSheet = await service.GetSheetAsync(UserId, 2027, 1);
        Assert.Single(janSheet.Transactions);
        Assert.Equal(txJanStart.Id, janSheet.Transactions[0].Id);
        Assert.Equal(25m, janSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);
    }

    [Fact]
    public async Task Transaction_TypeFollowsCategoryKind()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        BudgetCategory paycheck = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Paycheck");

        BudgetTransactionDto expenseTx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 10),
            Amount = 35m,
            Description = "Lunch",
            CategoryId = food.Id
        });

        BudgetTransactionDto incomeTx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 2500m,
            Description = "Paycheck 1",
            CategoryId = paycheck.Id
        });

        Assert.Equal(BudgetCategoryKind.Expense, expenseTx.Kind);
        Assert.Equal("Food", expenseTx.CategoryName);

        Assert.Equal(BudgetCategoryKind.Income, incomeTx.Kind);
        Assert.Equal("Paycheck", incomeTx.CategoryName);
    }

    [Fact]
    public async Task EditingTransactionDate_MovesItToNewMonthAutomatically()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

        // Create transaction in October
        BudgetTransactionDto tx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 20),
            Amount = 75m,
            Description = "Dinner",
            CategoryId = food.Id
        });

        // Move to November
        await service.UpdateTransactionAsync(UserId, tx.Id, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 11, 5),
            Amount = 75m,
            Description = "Dinner (moved)",
            CategoryId = food.Id
        });

        // Check October: should be empty
        MonthlyBudgetSheetDto octSheet = await service.GetSheetAsync(UserId, 2026, 10);
        Assert.Empty(octSheet.Transactions);
        Assert.Equal(0m, octSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);

        // Check November: should have the transaction
        MonthlyBudgetSheetDto novSheet = await service.GetSheetAsync(UserId, 2026, 11);
        Assert.Single(novSheet.Transactions);
        Assert.Equal(75m, novSheet.Expenses.Rows.Single(r => r.CategoryId == food.Id).Actual);
    }

    [Fact]
    public async Task CannotCreateOrAssignTransaction_ToArchivedCategory()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        BudgetCategoryDto customCat = await service.CreateCategoryAsync(UserId,
            new SaveCategoryRequest { Name = "Gym", Kind = BudgetCategoryKind.Expense, SortOrder = 100 });

        // Create transaction in active category first
        BudgetTransactionDto tx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 10),
            Amount = 30m,
            Description = "Initial snack",
            CategoryId = food.Id
        });

        // Archive custom category
        await service.DeleteCategoryAsync(UserId, customCat.Id);

        // 1. Attempt to create transaction in archived category -> ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTransactionAsync(UserId, new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 15),
                Amount = 50m,
                Description = "Membership",
                CategoryId = customCat.Id
            }));

        // 2. Attempt to update existing transaction to archived category -> ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateTransactionAsync(UserId, tx.Id, new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 10),
                Amount = 40m,
                Description = "Updated snack",
                CategoryId = customCat.Id
            }));
    }

    [Fact]
    public async Task Transaction_OwnershipIsolation_PreventsCrossUserAccess()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);
        await service.EnsureDefaultCategoriesAsync(OtherUserId);

        BudgetCategory myFood = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");
        BudgetCategory otherFood = await db.BudgetCategories.FirstAsync(c => c.UserId == OtherUserId && c.Name == "Food");

        // User creates transaction
        BudgetTransactionDto tx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 100m,
            Description = "My grocery",
            CategoryId = myFood.Id
        });

        // Other user cannot update or delete it
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateTransactionAsync(OtherUserId, tx.Id, new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 15),
                Amount = 200m,
                Description = "Hacked",
                CategoryId = otherFood.Id
            }));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteTransactionAsync(OtherUserId, tx.Id));

        // User cannot assign transaction to other user's category
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateTransactionAsync(UserId, new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 15),
                Amount = 50m,
                Description = "Cross user cat",
                CategoryId = otherFood.Id
            }));
    }

    [Fact]
    public async Task UpdateTransaction_SetsUpdatedAt_AndDeleteSoftDeletes()
    {
        await using AppDbContext db = CreateContext();
        var service = new MonthlyBudgetService(db);
        await service.EnsureDefaultCategoriesAsync(UserId);

        BudgetCategory food = await db.BudgetCategories.FirstAsync(c => c.UserId == UserId && c.Name == "Food");

        BudgetTransactionDto tx = await service.CreateTransactionAsync(UserId, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 10),
            Amount = 20m,
            Description = "Snack",
            CategoryId = food.Id
        });

        BudgetTransaction stored = await db.BudgetTransactions.SingleAsync(t => t.Id == tx.Id);
        Assert.Null(stored.UpdatedAt);

        await service.UpdateTransactionAsync(UserId, tx.Id, new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 11),
            Amount = 22m,
            Description = "Snack updated",
            CategoryId = food.Id
        });

        Assert.NotNull(stored.UpdatedAt);
        Assert.Equal(22m, stored.Amount);

        await service.DeleteTransactionAsync(UserId, tx.Id);
        BudgetTransaction deleted = await db.BudgetTransactions.IgnoreQueryFilters().SingleAsync(t => t.Id == tx.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }
}
