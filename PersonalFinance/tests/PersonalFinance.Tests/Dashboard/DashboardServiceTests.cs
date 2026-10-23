using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// Behaviour of the Dashboard service's summary, exercised against an in-memory database.
/// </summary>
public class DashboardServiceTests
{
    private const string UserId = "dashboard-user";
    private const string OtherUserId = "other-user";

    private static readonly string[] ExpectedGoalNamesNewestFirst = ["Newer", "Older"];

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Dashboard_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Task<DashboardSummaryDto> GetSummaryAsync(AppDbContext db, string userId = UserId)
    {
        return new DashboardService(db, TestBudgetDataProviderFactory.Create(db)).GetDashboardSummaryAsync(userId);
    }

    [Fact]
    public async Task EmptyAccount_ReturnsStructuredEmptySummary()
    {
        await using AppDbContext db = CreateContext();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.NotNull(summary);
        Assert.Null(summary.NetWorth);
        Assert.Null(summary.Budget);
        Assert.Null(summary.MonthlyBudget);
        Assert.Empty(summary.SavingsGoals);
        Assert.Empty(summary.RecentActivity);
    }

    [Fact]
    public async Task Budget_WhenTwoAllocationsShareThePeriod_UsesTheNewestRecord()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetAllocations.AddRange(
            new UserBudgetAllocation { Id = 1, UserId = UserId, Period = "2026-10", MonthlyIncome = 4_000m },
            new UserBudgetAllocation { Id = 2, UserId = UserId, Period = "2026-10", MonthlyIncome = 5_000m },
            new UserBudgetAllocation { Id = 3, UserId = UserId, Period = "2026-09", MonthlyIncome = 9_000m });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(5_000m, summary.Budget!.MonthlyIncome);
    }

    [Fact]
    public async Task SavingsGoals_WhenCreatedAtTies_OrdersNewestRecordFirst()
    {
        await using AppDbContext db = CreateContext();
        var createdAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        db.SavingsGoals.AddRange(
            new SavingsGoal { Id = 1, UserId = UserId, Name = "Older", TargetAmount = 100m, CreatedAt = createdAt },
            new SavingsGoal { Id = 2, UserId = UserId, Name = "Newer", TargetAmount = 100m, CreatedAt = createdAt });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(ExpectedGoalNamesNewestFirst, summary.SavingsGoals.Select(g => g.Name));
    }

    [Fact]
    public async Task Summary_ExcludesOtherUsersAndSoftDeletedRecords()
    {
        await using AppDbContext db = CreateContext();
        db.NetWorthParameters.Add(new NetWorthParameters { UserId = OtherUserId, StartingBalance = 1m });
        db.SavingsGoals.AddRange(
            new SavingsGoal { UserId = OtherUserId, Name = "Not mine", TargetAmount = 100m },
            new SavingsGoal { UserId = UserId, Name = "Deleted", TargetAmount = 100m, IsDeleted = true });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Null(summary.NetWorth);
        Assert.Empty(summary.SavingsGoals);
        Assert.Empty(summary.RecentActivity);
    }

    [Fact]
    public async Task Summary_DefaultSource_WhenSnapshotsExist_IsGoogleSheet()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 1,
            Year = 2026,
            MonthNumber = 10,
            MonthName = "October",
            BudgetedIncome = 5000m,
            ActualIncome = 5200m
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(BudgetDataSource.GoogleSheet, summary.ActiveSource);
        Assert.NotNull(summary.MonthlyBudget);
        Assert.Equal(5000m, summary.MonthlyBudget.BudgetedIncome);
        Assert.Equal(5200m, summary.MonthlyBudget.ActualIncome);
    }

    [Fact]
    public async Task Summary_DefaultSource_WhenNoSnapshotsExist_IsBudgetTracker()
    {
        await using AppDbContext db = CreateContext();
        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 10 };
        var category = new BudgetCategory { UserId = UserId, Name = "Salary", Kind = BudgetCategoryKind.Income };
        db.MonthlyBudgetPlans.Add(plan);
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();

        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine
        {
            PlanId = plan.Id,
            CategoryId = category.Id,
            PlannedAmount = 4500m
        });
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = category.Id,
            Date = new DateOnly(2026, 10, 15),
            Amount = 4600m,
            Description = "Paycheck"
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(BudgetDataSource.BudgetTracker, summary.ActiveSource);
        Assert.NotNull(summary.MonthlyBudget);
        Assert.Equal(4500m, summary.MonthlyBudget.BudgetedIncome);
        Assert.Equal(4600m, summary.MonthlyBudget.ActualIncome);
    }

    [Fact]
    public async Task Summary_WhenExplicitPreferenceSet_SwitchesActiveSourceAndTransactions()
    {
        await using AppDbContext db = CreateContext();

        var snapshot = new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 1,
            Year = 2026,
            MonthNumber = 10,
            MonthName = "October",
            BudgetedIncome = 5000m,
            ActualIncome = 5200m,
            BudgetedExpenses = 3000m,
            ActualExpenses = 2800m
        };
        db.SpreadsheetBudgetSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
        {
            UserId = UserId,
            SnapshotId = snapshot.Id,
            Date = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m,
            Category = "Sheet Cat",
            Description = "Sheet TX",
            Type = "Expense"
        });

        var expenseCat = new BudgetCategory
        {
            UserId = UserId,
            Name = "Tracker Cat",
            Kind = BudgetCategoryKind.Expense
        };
        var incomeCat = new BudgetCategory
        {
            UserId = UserId,
            Name = "Salary",
            Kind = BudgetCategoryKind.Income
        };
        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 10 };
        db.BudgetCategories.AddRange(expenseCat, incomeCat);
        db.MonthlyBudgetPlans.Add(plan);
        await db.SaveChangesAsync();

        db.MonthlyBudgetLines.AddRange(
            new MonthlyBudgetLine { PlanId = plan.Id, CategoryId = incomeCat.Id, PlannedAmount = 4500m },
            new MonthlyBudgetLine { PlanId = plan.Id, CategoryId = expenseCat.Id, PlannedAmount = 800m });
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = expenseCat.Id,
            Date = new DateOnly(2026, 10, 20),
            Amount = 250m,
            Description = "Tracker TX"
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto defaultSummary = await GetSummaryAsync(db);
        Assert.Equal(BudgetDataSource.GoogleSheet, defaultSummary.ActiveSource);
        Assert.Contains(defaultSummary.RecentActivity, a => a.Title == "Sheet TX");
        Assert.DoesNotContain(defaultSummary.RecentActivity, a => a.Title == "Tracker TX");
        Assert.NotNull(defaultSummary.MonthlyBudget);
        Assert.Equal(5000m, defaultSummary.MonthlyBudget.BudgetedIncome);
        Assert.Equal(5200m, defaultSummary.MonthlyBudget.ActualIncome);
        Assert.Equal(3000m, defaultSummary.MonthlyBudget.BudgetedExpenses);
        Assert.Equal(2800m, defaultSummary.MonthlyBudget.ActualExpenses);

        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto switchedSummary = await GetSummaryAsync(db);
        Assert.Equal(BudgetDataSource.BudgetTracker, switchedSummary.ActiveSource);
        Assert.Contains(switchedSummary.RecentActivity, a => a.Title == "Tracker TX");
        Assert.DoesNotContain(switchedSummary.RecentActivity, a => a.Title == "Sheet TX");
        Assert.NotNull(switchedSummary.MonthlyBudget);
        Assert.Equal(4500m, switchedSummary.MonthlyBudget.BudgetedIncome);
        Assert.Equal(0m, switchedSummary.MonthlyBudget.ActualIncome);
        Assert.Equal(800m, switchedSummary.MonthlyBudget.BudgetedExpenses);
        Assert.Equal(250m, switchedSummary.MonthlyBudget.ActualExpenses);
    }

    [Fact]
    public async Task Summary_WhenActiveSourceHasNoMonth_MonthlyBudgetIsNullAndDoesNotUsePlanner()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetAllocations.Add(new UserBudgetAllocation
        {
            UserId = UserId,
            Period = "2026-10",
            MonthlyIncome = 7000m
        });
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.GoogleSheet
        });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(BudgetDataSource.GoogleSheet, summary.ActiveSource);
        Assert.Equal(7000m, summary.Budget!.MonthlyIncome);
        Assert.Null(summary.MonthlyBudget);
    }

    [Fact]
    public async Task Summary_RecentActivity_WhenBudgetTracker_MapsIncomeAndExpenseSigns()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });

        var expenseCat = new BudgetCategory { UserId = UserId, Name = "Groceries", Kind = BudgetCategoryKind.Expense };
        var incomeCat = new BudgetCategory { UserId = UserId, Name = "Paycheck", Kind = BudgetCategoryKind.Income };
        db.BudgetCategories.AddRange(expenseCat, incomeCat);
        await db.SaveChangesAsync();

        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = expenseCat.Id,
                Date = new DateOnly(2026, 10, 5),
                Amount = 75.50m,
                Description = "Supermarket"
            },
            new BudgetTransaction
            {
                UserId = UserId,
                CategoryId = incomeCat.Id,
                Date = new DateOnly(2026, 10, 6),
                Amount = 2000m,
                Description = "Salary"
            }
        );
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await GetSummaryAsync(db);

        Assert.Equal(2, summary.RecentActivity.Count);
        RecentActivityDto salary = summary.RecentActivity.Single(a => a.Title == "Salary");
        Assert.Equal("Income", salary.Type);
        Assert.Equal(new DateTime(2026, 10, 6), salary.Date);

        RecentActivityDto groceries = summary.RecentActivity.Single(a => a.Title == "Supermarket");
        Assert.Equal("Expense", groceries.Type);
        Assert.Equal(new DateTime(2026, 10, 5), groceries.Date);
    }
}
