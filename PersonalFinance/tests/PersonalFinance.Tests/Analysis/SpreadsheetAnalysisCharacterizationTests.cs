using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using CreateNotificationRequestDto = PersonalFinance.ApiService.Infrastructure.CreateNotificationRequestDto;
using NotificationDto = PersonalFinance.ApiService.Infrastructure.NotificationDto;

namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// Characterization tests capturing the existing read behaviour of SpreadsheetAnalysisService,
/// BudgetService alerts, and DashboardService recent transactions against synced spreadsheet entities.
/// </summary>
public class SpreadsheetAnalysisCharacterizationTests
{
    private const string UserId = "char-user";
    private static readonly int[] ExpectedYears2026And2025 = [2026, 2025];
    private static readonly int[] ExpectedMonthsJanuaryAndOctober = [1, 10];
    private static readonly int[] ExpectedYears2026Only = [2026];

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CharDb_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static SpreadsheetAnalysisService CreateService(AppDbContext db,
        INotificationService? notificationService = null)
    {
        notificationService ??= new FakeNotificationService();
        return new SpreadsheetAnalysisService(db, new FakeSpreadsheetReader(), notificationService,
            TestBudgetDataProviderFactory.Create(db), NullLogger<SpreadsheetAnalysisService>.Instance);
    }

    private static SpreadsheetAnalysisController CreateAnalysisController(AppDbContext db)
    {
        return new SpreadsheetAnalysisController(
            CreateService(db),
            new FakeSpreadsheetReader(),
            NullLogger<SpreadsheetAnalysisController>.Instance,
            new BudgetDataSourceService(db),
            new GoogleSheetRemovalService(db, new BudgetDataSourceService(db), TimeProvider.System))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, UserId)
                    }))
                }
            }
        };
    }

    private static SpreadsheetBudgetSnapshot CreateSnapshot(
        string userId = UserId,
        int year = 2026,
        int month = 10,
        string monthName = "October",
        decimal budgetedIncome = 5000m,
        decimal actualIncome = 5500m,
        decimal budgetedExpenses = 4000m,
        decimal actualExpenses = 3800m,
        string categoryJson =
            "[{\"categoryName\":\"Food\",\"budgetedAmount\":500,\"actualAmount\":450},{\"categoryName\":\"Rent\",\"budgetedAmount\":1500,\"actualAmount\":1500}]")
    {
        return new SpreadsheetBudgetSnapshot
        {
            UserId = userId,
            ConnectionId = 1,
            Year = year,
            MonthNumber = month,
            MonthName = monthName,
            BudgetedIncome = budgetedIncome,
            ActualIncome = actualIncome,
            BudgetedExpenses = budgetedExpenses,
            ActualExpenses = actualExpenses,
            CategoryBreakdownJson = categoryJson
        };
    }

    [Fact]
    public async Task GetSummaryStatisticsAsync_ReturnsAggregatedMetrics()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot());
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.SummaryStatisticsDto? stats = await service.GetSummaryStatisticsAsync(UserId, 2026, 10);

        Assert.NotNull(stats);
        Assert.Equal(2026, stats.Year);
        Assert.Equal(10, stats.MonthNumber);
        Assert.Equal("October", stats.MonthName);
        Assert.Equal(5500m, stats.TotalIncome);
        Assert.Equal(3800m, stats.TotalExpenses);
        Assert.Equal(1700m, stats.NetSavings);
        Assert.Equal(30.9m, stats.SavingsRate); // (1700 / 5500) * 100 = 30.909...
        Assert.Equal(1, stats.SnapshotCount);
    }

    [Fact]
    public async Task GetTrendDataAsync_ReturnsAllMonthsInYearSorted()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.AddRange(
            CreateSnapshot(year: 2026, month: 11, monthName: "November", actualIncome: 6000m, actualExpenses: 4000m),
            CreateSnapshot(year: 2026, month: 10, monthName: "October", actualIncome: 5000m, actualExpenses: 3000m));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.TrendDataDto trend = await service.GetTrendDataAsync(UserId, 2026);

        Assert.Equal(2026, trend.Year);
        Assert.Equal(2, trend.TrendPoints.Count);
        Assert.Equal(10, trend.TrendPoints[0].MonthNumber);
        Assert.Equal(11, trend.TrendPoints[1].MonthNumber);
        Assert.Equal(2000m, trend.TrendPoints[0].NetSavings);
        Assert.Equal(40.0m, trend.TrendPoints[0].SavingsRate);
    }

    [Fact]
    public async Task GetCategoryInsightsAsync_ParsesCategoryBreakdownJson()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(categoryJson:
            "[{\"categoryName\":\"Dining\",\"budgetedAmount\":300,\"actualAmount\":400},{\"categoryName\":\"Utilities\",\"budgetedAmount\":200,\"actualAmount\":150}]"));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.CategoryInsightsDto? insights = await service.GetCategoryInsightsAsync(UserId, 2026, 10);

        Assert.NotNull(insights);
        Assert.Equal(2, insights.Categories.Count);
        // Ordered descending by ActualAmount: Dining (400), Utilities (150)
        Assert.Equal("Dining", insights.Categories[0].CategoryName);
        Assert.Equal(400m, insights.Categories[0].ActualAmount);
        Assert.Equal(300m, insights.Categories[0].BudgetedAmount);
        Assert.Equal(-100m, insights.Categories[0].Variance);
        Assert.Equal(133.3, insights.Categories[0].PercentageUsed);
        Assert.Equal("Over Budget", insights.Categories[0].Status);

        Assert.Equal("Utilities", insights.Categories[1].CategoryName);
        Assert.Equal(150m, insights.Categories[1].ActualAmount);
        Assert.Equal(50m, insights.Categories[1].Variance);
        Assert.Equal(75.0, insights.Categories[1].PercentageUsed);
        Assert.Equal("Within Budget", insights.Categories[1].Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task GetCategoryInsightsAsync_CarriesTheMonthsOwnIncomeAndExpenses(int? month)
    {
        // With no month selected the summary sums the year, so Money Flow needs the insights' own month totals.
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(month: 1, monthName: "January", actualIncome: 9000m,
            actualExpenses: 8000m));
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(actualIncome: 5500m, actualExpenses: 3800m));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.CategoryInsightsDto? insights =
            await service.GetCategoryInsightsAsync(UserId, 2026, month);

        Assert.NotNull(insights);
        Assert.Equal(10, insights.MonthNumber);
        Assert.Equal(5500m, insights.ActualIncome);
        Assert.Equal(3800m, insights.ActualExpenses);
    }

    [Fact]
    public async Task GetBudgetVarianceAsync_RanksCategoriesByVariance()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(categoryJson:
            "[{\"categoryName\":\"Groceries\",\"budgetedAmount\":500,\"actualAmount\":600},{\"categoryName\":\"Travel\",\"budgetedAmount\":1000,\"actualAmount\":800}]"));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.BudgetVarianceDto? variance = await service.GetBudgetVarianceAsync(UserId, 2026, 10);

        Assert.NotNull(variance);
        Assert.Equal(2, variance.Rankings.Count);
        // Ordered by ActualAmount - BudgetedAmount descending: Groceries (+100), Travel (-200)
        Assert.Equal(1, variance.Rankings[0].Rank);
        Assert.Equal("Groceries", variance.Rankings[0].CategoryName);
        Assert.Equal(-100m, variance.Rankings[0].Variance);
        Assert.Equal("Over Budget", variance.Rankings[0].Status);

        Assert.Equal(2, variance.Rankings[1].Rank);
        Assert.Equal("Travel", variance.Rankings[1].CategoryName);
        Assert.Equal(200m, variance.Rankings[1].Variance);
        Assert.Equal("Under Budget", variance.Rankings[1].Status);
    }

    [Fact]
    public async Task GetAvailableYearsAndMonths_ReturnsDistinctSortedLists()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.AddRange(
            CreateSnapshot(year: 2025, month: 12),
            CreateSnapshot(year: 2026, month: 1),
            CreateSnapshot(year: 2026, month: 10));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        List<int> years = await service.GetAvailableYearsAsync(UserId);
        List<int> months2026 = await service.GetAvailableMonthsAsync(UserId, 2026);
        (int year, int month)? mostRecent = await service.GetMostRecentMonthAsync(UserId);

        Assert.Equal(ExpectedYears2026And2025, years);
        Assert.Equal(ExpectedMonthsJanuaryAndOctober, months2026);
        Assert.Equal((2026, 10), mostRecent);
    }

    [Fact]
    public async Task MultiSnapshot_SameMonth_AggregatesTotalsAndMergesCategoriesCaseInsensitively()
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
                ActualIncome = 3200m,
                BudgetedExpenses = 2000m,
                ActualExpenses = 1800m,
                StartingBalance = 10000m,
                EndingBalance = 11400m,
                CategoryBreakdownJson =
                    "[{\"categoryName\":\"Food\",\"budgetedAmount\":500,\"actualAmount\":400},{\"categoryName\":\"Rent\",\"budgetedAmount\":1000,\"actualAmount\":1000}]"
            },
            new SpreadsheetBudgetSnapshot
            {
                UserId = UserId,
                ConnectionId = 2,
                Year = 2026,
                MonthNumber = 10,
                MonthName = "October",
                BudgetedIncome = 2000m,
                ActualIncome = 2100m,
                BudgetedExpenses = 1500m,
                ActualExpenses = 1400m,
                StartingBalance = 5000m,
                EndingBalance = 5700m,
                CategoryBreakdownJson =
                    "[{\"categoryName\":\"food\",\"budgetedAmount\":300,\"actualAmount\":250},{\"categoryName\":\"Utilities\",\"budgetedAmount\":200,\"actualAmount\":150}]"
            });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);

        // 1. Summary Statistics aggregates totals across snapshots
        PersonalFinance.ApiService.Features.Analysis.SummaryStatisticsDto? stats = await service.GetSummaryStatisticsAsync(UserId, 2026, 10);
        Assert.NotNull(stats);
        Assert.Equal(5300m, stats.TotalIncome); // 3200 + 2100
        Assert.Equal(3200m, stats.TotalExpenses); // 1800 + 1400
        Assert.Equal(2100m, stats.NetSavings);
        Assert.Equal(2, stats.SnapshotCount);

        // 2. Category Insights merges "Food" and "food" case-insensitively
        PersonalFinance.ApiService.Features.Analysis.CategoryInsightsDto? insights = await service.GetCategoryInsightsAsync(UserId, 2026, 10);
        Assert.NotNull(insights);
        Assert.Equal(3, insights.Categories.Count); // Food, Rent, Utilities

        PersonalFinance.ApiService.Features.Analysis.CategoryBreakdownDto food = insights.Categories.Single(c => c.CategoryName.Equals("Food", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(800m, food.BudgetedAmount); // 500 + 300
        Assert.Equal(650m, food.ActualAmount); // 400 + 250

        // 3. Budget Variance merges categories
        PersonalFinance.ApiService.Features.Analysis.BudgetVarianceDto? variance = await service.GetBudgetVarianceAsync(UserId, 2026, 10);
        Assert.NotNull(variance);
        Assert.Equal(3, variance.Rankings.Count);
    }

    [Fact]
    public async Task GetSpendingVelocityAsync_ForAPastMonth_SpreadsItsSpendOverEveryDay()
    {
        // A past month, so the result does not depend on today: the service reads DateTime.UtcNow for the current month.
        await using AppDbContext db = CreateContext();
        SpreadsheetBudgetSnapshot snapshot = CreateSnapshot(year: 2025, month: 10, budgetedExpenses: 3100m, actualExpenses: 1550m);
        db.SpreadsheetBudgetSnapshots.Add(snapshot);

        // Add transaction records for weekly trend and category velocities
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2025, 10, 5, 12, 0, 0, DateTimeKind.Utc),
                Amount = 100m,
                Description = "Grocery 1",
                Category = "Food",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2025, 10, 12, 12, 0, 0, DateTimeKind.Utc),
                Amount = 200m,
                Description = "Grocery 2",
                Category = "Food",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                UserId = UserId,
                SnapshotId = snapshot.Id,
                Date = new DateTime(2025, 10, 15, 12, 0, 0, DateTimeKind.Utc),
                Amount = 500m,
                Description = "Bonus",
                Category = "Work",
                Type = "Income" // Should be excluded from weekly expense trend
            });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.SpendingVelocityDto? velocity = await service.GetSpendingVelocityAsync(UserId, 2025, 10);

        Assert.NotNull(velocity);
        Assert.Equal(2025, velocity.Year);
        Assert.Equal(10, velocity.MonthNumber);
        Assert.Equal(3100m, velocity.BudgetedExpenses);
        Assert.Equal(31.0, velocity.DaysElapsed);
        Assert.Equal(0.0, velocity.DaysRemaining);
        Assert.Equal(50.00m, velocity.DailyBurnRate); // 1,550 / 31 days
        Assert.Equal(1550.00m, velocity.ProjectedMonthlySpend);
        Assert.Equal(50.0, velocity.BudgetConsumptionPercent); // 1,550 / 3,100
        Assert.Equal(100.0, velocity.TimeElapsedPercent);
        Assert.Equal("Behind Schedule", velocity.Status); // 50% spent with all of the month gone
        // Days 1-7, 8-14, 15-21, 22-28, 29-31; the income on the 15th is left out.
        Assert.Equal([100m, 200m, 0m, 0m, 0m], velocity.WeeklyTrend.Select(w => w.TotalSpent));
        Assert.Equal([("Rent", 100.0, "On Track"), ("Food", 90.0, "On Track")],
            velocity.CategoryVelocities.Select(c => (c.CategoryName, c.ConsumptionPercent, c.Status)));
    }

    [Fact]
    public async Task CategoryBreakdown_WithInvalidJson_ReturnsEmptyCategoriesWithoutFailing()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(categoryJson: "INVALID-JSON-{{{"));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.CategoryInsightsDto? insights = await service.GetCategoryInsightsAsync(UserId, 2026, 10);
        PersonalFinance.ApiService.Features.Analysis.BudgetVarianceDto? variance = await service.GetBudgetVarianceAsync(UserId, 2026, 10);

        Assert.NotNull(insights);
        Assert.Empty(insights.Categories);
        Assert.Equal(0m, insights.TotalSpent);

        Assert.NotNull(variance);
        Assert.Empty(variance.Rankings);
    }

    [Fact]
    public async Task UserIsolation_OtherUsersDataNeverReturned()
    {
        const string otherUserId = "other-char-user";
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.AddRange(
            CreateSnapshot(userId: UserId, year: 2026, month: 10, actualIncome: 5000m, actualExpenses: 3000m),
            CreateSnapshot(userId: otherUserId, year: 2026, month: 10, actualIncome: 99999m, actualExpenses: 88888m));
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.SummaryStatisticsDto? stats = await service.GetSummaryStatisticsAsync(UserId, 2026, 10);

        Assert.NotNull(stats);
        Assert.Equal(5000m, stats.TotalIncome);
        Assert.Equal(3000m, stats.TotalExpenses);
        Assert.Equal(1, stats.SnapshotCount);

        List<int> years = await service.GetAvailableYearsAsync(UserId);
        Assert.Equal(ExpectedYears2026Only, years);
    }

    [Fact]
    public async Task BudgetService_CheckBudgetAlerts_CreatesExceededNotificationWhen100PercentOrMore()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(budgetedExpenses: 1000m, actualExpenses: 1100m)); // 110%
        await db.SaveChangesAsync();

        var notifService = new FakeNotificationService();
        var budgetService = new BudgetService(db, NullLogger<BudgetService>.Instance, notifService,
            TestBudgetDataProviderFactory.Create(db));

        await budgetService.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            Period = "2026-10",
            MonthlyIncome = 5000m,
            Allocations = new BudgetAllocationsDto { Housing = 100m }
        });

        Assert.Single(notifService.Notifications);
        Assert.Equal("Budget exceeded", notifService.Notifications[0].Title);
        Assert.Equal("BudgetAlert", notifService.Notifications[0].Type);
    }

    [Fact]
    public async Task BudgetService_CheckBudgetAlerts_CreatesNotificationWhenThresholdExceeded()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetBudgetSnapshots.Add(CreateSnapshot(budgetedExpenses: 1000m, actualExpenses: 900m)); // 90%
        await db.SaveChangesAsync();

        var notifService = new FakeNotificationService();
        var budgetService = new BudgetService(db, NullLogger<BudgetService>.Instance, notifService,
            TestBudgetDataProviderFactory.Create(db));

        await budgetService.SaveBudgetAsync(UserId, new SaveBudgetRequestDto
        {
            Period = "2026-10",
            MonthlyIncome = 5000m,
            Allocations = new BudgetAllocationsDto { Housing = 100m }
        });

        Assert.Single(notifService.Notifications);
        Assert.Equal("Budget warning", notifService.Notifications[0].Title);
        Assert.Equal("BudgetAlert", notifService.Notifications[0].Type);
    }

    [Fact]
    public async Task GetTrendDataAsync_WithMultipleSnapshotsInSameMonth_ReturnsOnePointPerSnapshot()
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
            });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.TrendDataDto trend = await service.GetTrendDataAsync(UserId, 2026);

        // Characterize existing behaviour: returns one trend point per stored snapshot row
        Assert.Equal(2, trend.TrendPoints.Count);
        Assert.Equal(10, trend.TrendPoints[0].MonthNumber);
        Assert.Equal(10, trend.TrendPoints[1].MonthNumber);
        Assert.Equal(3000m, trend.TrendPoints[0].Income);
        Assert.Equal(2500m, trend.TrendPoints[1].Income);
    }

    [Fact]
    public async Task GetSpendingVelocityAsync_WithMultipleSnapshotsInSameMonth_CalculatesCorrectVelocity()
    {
        await using AppDbContext db = CreateContext();
        var snapshot1 = new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 1,
            Year = 2026,
            MonthNumber = 10,
            MonthName = "October",
            BudgetedExpenses = 3000m,
            ActualExpenses = 1200m,
            CategoryBreakdownJson = "[{\"categoryName\":\"Groceries\",\"budgetedAmount\":600,\"actualAmount\":200}]"
        };
        var snapshot2 = new SpreadsheetBudgetSnapshot
        {
            UserId = UserId,
            ConnectionId = 2,
            Year = 2026,
            MonthNumber = 10,
            MonthName = "October",
            BudgetedExpenses = 1000m,
            ActualExpenses = 600m,
            CategoryBreakdownJson = "[{\"categoryName\":\"Utilities\",\"budgetedAmount\":400,\"actualAmount\":300}]"
        };
        db.SpreadsheetBudgetSnapshots.AddRange(snapshot1, snapshot2);

        // Add transaction records for weekly trend
        db.SpreadsheetTransactionRecords.AddRange(
            new SpreadsheetTransactionRecord
            {
                UserId = UserId,
                SnapshotId = snapshot1.Id,
                Date = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                Amount = 100m,
                Category = "Groceries",
                Type = "Expense"
            },
            new SpreadsheetTransactionRecord
            {
                UserId = UserId,
                SnapshotId = snapshot2.Id,
                Date = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc),
                Amount = 150m,
                Category = "Utilities",
                Type = "Expense"
            }
        );
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.SpendingVelocityDto? velocity = await service.GetSpendingVelocityAsync(UserId, 2026, 10);

        Assert.NotNull(velocity);
        // Sum of budgeted expenses: 3000 + 1000 = 4000
        Assert.Equal(4000m, velocity.BudgetedExpenses);
        // Categories merged: Groceries and Utilities
        Assert.Equal(2, velocity.CategoryVelocities.Count);
        Assert.Contains(velocity.CategoryVelocities, c => c.CategoryName == "Groceries" && c.BudgetedAmount == 600m);
        Assert.Contains(velocity.CategoryVelocities, c => c.CategoryName == "Utilities" && c.BudgetedAmount == 400m);
        // Weekly trend contains transactions from both snapshots
        Assert.NotEmpty(velocity.WeeklyTrend);
    }

    [Fact]
    public async Task SpreadsheetAnalysis_WhenSourceIsBudgetTracker_ReturnsTrackerSummaryAndVelocity()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });

        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 10 };
        var category = new BudgetCategory { UserId = UserId, Name = "Dining", Kind = BudgetCategoryKind.Expense };
        db.MonthlyBudgetPlans.Add(plan);
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();

        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine
        {
            PlanId = plan.Id,
            CategoryId = category.Id,
            PlannedAmount = 600m
        });
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = category.Id,
            Date = new DateOnly(2026, 10, 8),
            Amount = 150m,
            Description = "Dinner"
        });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisService service = CreateService(db);
        PersonalFinance.ApiService.Features.Analysis.SummaryStatisticsDto? stats = await service.GetSummaryStatisticsAsync(UserId, 2026, 10);
        Assert.NotNull(stats);
        Assert.Equal(150m, stats.TotalExpenses);
        Assert.Equal(1, stats.SnapshotCount);

        PersonalFinance.ApiService.Features.Analysis.SpendingVelocityDto? velocity = await service.GetSpendingVelocityAsync(UserId, 2026, 10);
        Assert.NotNull(velocity);
        Assert.Equal(600m, velocity.BudgetedExpenses);
        Assert.Single(velocity.CategoryVelocities);
        Assert.Equal("Dining", velocity.CategoryVelocities[0].CategoryName);
        Assert.NotEmpty(velocity.WeeklyTrend);

        (int year, int month)? mostRecent = await service.GetMostRecentMonthAsync(UserId);
        Assert.NotNull(mostRecent);
        Assert.Equal(2026, mostRecent.Value.year);
        Assert.Equal(10, mostRecent.Value.month);
    }

    [Fact]
    public async Task SpreadsheetAnalysisController_GetDashboard_ReturnsActiveSourceAndMatchesProviderOutput()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });

        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 10 };
        var category = new BudgetCategory { UserId = UserId, Name = "Rent", Kind = BudgetCategoryKind.Expense };
        db.MonthlyBudgetPlans.Add(plan);
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();

        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine
        {
            PlanId = plan.Id,
            CategoryId = category.Id,
            PlannedAmount = 1200m
        });
        db.BudgetTransactions.Add(new BudgetTransaction
        {
            UserId = UserId,
            CategoryId = category.Id,
            Date = new DateOnly(2026, 10, 1),
            Amount = 1200m,
            Description = "Monthly Rent"
        });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisController controller = CreateAnalysisController(db);

        IActionResult actionResult = await controller.GetDashboard(2026, 10);
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(actionResult);
        AnalysisDashboardResult result = Assert.IsType<AnalysisDashboardResult>(okResult.Value);

        Assert.True(result.HasData);
        Assert.Equal(BudgetDataSource.BudgetTracker, result.ActiveSource);
        Assert.Equal(2026, result.Year);
        Assert.Equal(10, result.Month);
        Assert.NotNull(result.Summary);
        Assert.Equal(1200m, result.Summary.TotalExpenses);
    }

    [Fact]
    public async Task SpreadsheetAnalysisController_GetDashboard_WhenEmpty_ReturnsHasDataFalseWithActiveSource()
    {
        await using AppDbContext db = CreateContext();

        SpreadsheetAnalysisController controller = CreateAnalysisController(db);

        IActionResult actionResult = await controller.GetDashboard(2026, 10);
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(actionResult);
        AnalysisDashboardResult result = Assert.IsType<AnalysisDashboardResult>(okResult.Value);

        Assert.False(result.HasData);
        Assert.Equal(BudgetDataSource.BudgetTracker, result.ActiveSource);
        Assert.Equal(2026, result.Year);
        Assert.Equal(10, result.Month);
        Assert.Empty(result.AvailableYears);
        Assert.Empty(result.AvailableMonths);
    }

    [Fact]
    public async Task
        SpreadsheetAnalysisController_GetDashboard_WhenRequestedMonthMissing_KeepsYearMonthAndListsOtherMonths()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference
        {
            UserId = UserId,
            Source = BudgetDataSource.BudgetTracker
        });

        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 10 };
        var category = new BudgetCategory
        {
            UserId = UserId,
            Name = "Rent",
            Kind = BudgetCategoryKind.Expense
        };
        db.MonthlyBudgetPlans.Add(plan);
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();

        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine
        {
            PlanId = plan.Id,
            CategoryId = category.Id,
            PlannedAmount = 1200m
        });
        await db.SaveChangesAsync();

        SpreadsheetAnalysisController controller = CreateAnalysisController(db);

        IActionResult actionResult = await controller.GetDashboard(2026, 3);
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(actionResult);
        AnalysisDashboardResult result = Assert.IsType<AnalysisDashboardResult>(okResult.Value);

        Assert.False(result.HasData);
        Assert.Null(result.Summary);
        Assert.Equal(BudgetDataSource.BudgetTracker, result.ActiveSource);
        Assert.Equal(2026, result.Year);
        Assert.Equal(3, result.Month);
        Assert.Contains(2026, result.AvailableYears);
        Assert.Contains(10, result.AvailableMonths);
        Assert.DoesNotContain(3, result.AvailableMonths);
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<CreateNotificationRequestDto> Notifications { get; } = new();

        public Task<int> CreateNotificationAsync(string userId, CreateNotificationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(request);
            return Task.FromResult(Notifications.Count);
        }

        public Task<List<NotificationDto>> GetNotificationsAsync(string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> MarkAsReadAsync(string userId, int notificationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSpreadsheetReader : ISpreadsheetReader
    {
        public Task<MonthlyBudgetReportDto> GetSpreadsheetBudgetReportAsync(string userId, string fileId, string? fileName = null, int? connectionId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SpreadsheetTransactionsDto> GetSpreadsheetTransactionsAsync(string userId, string fileId, string? fileName = null, int? connectionId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
