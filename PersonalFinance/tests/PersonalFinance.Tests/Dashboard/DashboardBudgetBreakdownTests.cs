using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// Behaviour of the Dashboard budget breakdown: category allocations in dollars and percent,
/// the Unallocated remainder, and selection of the most recent budget period.
/// </summary>
public class DashboardBudgetBreakdownTests
{
    private const string UserId = "budget-user";
    private const string OtherUserId = "other-budget-user";

    private static readonly string[] ExpectedCategoryOrder =
    [
        "Housing", "Food", "Transportation", "Utilities", "Insurance", "Healthcare", "Savings",
        "Debt Repayment", "Entertainment", "Miscellaneous"
    ];

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardBudget_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<BudgetSummaryDto?> GetBudgetAsync(AppDbContext db, string userId = UserId)
    {
        DashboardSummaryDto summary =
            await new DashboardService(db, TestBudgetDataProviderFactory.Create(db)).GetDashboardSummaryAsync(userId);
        return summary.Budget;
    }

    /// <summary>A $5,000 budget whose ten category amounts total exactly $5,000 (100% of income).</summary>
    private static UserBudgetAllocation FullyAllocatedBudget(string userId = UserId, string period = "2026-10") => new()
    {
        UserId = userId,
        Period = period,
        MonthlyIncome = 5_000m,
        HousingAmount = 1_500m,
        FoodAmount = 625m,
        TransportationAmount = 500m,
        UtilitiesAmount = 250m,
        InsuranceAmount = 250m,
        HealthcareAmount = 250m,
        SavingsAmount = 750m,
        DebtRepaymentAmount = 375m,
        EntertainmentAmount = 300m,
        MiscellaneousAmount = 200m
    };

    [Fact]
    public async Task Allocations_ConvertEachPercentToDollarsOfMonthlyIncome()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetAllocations.Add(FullyAllocatedBudget());
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        var amounts = budget!.Allocations.ToDictionary(a => a.Name, a => a.Amount);
        Assert.Equal(1_500m, amounts["Housing"]);
        Assert.Equal(625m, amounts["Food"]);
        Assert.Equal(375m, amounts["Debt Repayment"]);
        Assert.Equal(200m, amounts["Miscellaneous"]);
    }

    [Fact]
    public async Task Percent_IsDerivedFromTheStoredAmount_ForAnExactAmountThatIsNotAWholePercent()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation allocation = FullyAllocatedBudget();
        allocation.MonthlyIncome = 3_700m;
        allocation.HousingAmount = 700m;
        db.UserBudgetAllocations.Add(allocation);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        BudgetAllocationDto housing = budget!.Allocations.Single(a => a.Name == "Housing");
        Assert.Equal(700m, housing.Amount);
        Assert.Equal(18.92m, Math.Round(housing.Percent, 2));
        Assert.Equal(18.92m, Math.Round(budget.HousingPercent!.Value, 2));
    }

    [Fact]
    public async Task Percent_IsZeroForEveryCategory_WhenMonthlyIncomeIsZero()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation allocation = FullyAllocatedBudget();
        allocation.MonthlyIncome = 0m;
        db.UserBudgetAllocations.Add(allocation);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.All(budget!.Allocations, a => Assert.Equal(0m, a.Percent));
        Assert.Equal(0m, budget.HousingPercent);
        Assert.Equal(0m, budget.TotalAllocatedPercent);
    }

    [Fact]
    public async Task Allocations_ListAllTenCategoriesInBudgetPageOrder_IncludingZeroOnes()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation allocation = FullyAllocatedBudget();
        allocation.EntertainmentAmount = 0m;
        db.UserBudgetAllocations.Add(allocation);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.Equal(ExpectedCategoryOrder, budget!.Allocations.Select(a => a.Name));
        BudgetAllocationDto entertainment = budget.Allocations.Single(a => a.Name == "Entertainment");
        Assert.Equal(0m, entertainment.Percent);
        Assert.Equal(0m, entertainment.Amount);
    }

    [Fact]
    public async Task Unallocated_WhenAllocationsTotalUnder100Percent_IsTheRemainderInPercentAndDollars()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation allocation = FullyAllocatedBudget();
        allocation.HousingAmount = 1_000m; // 20% of 5,000, so total is now 90%
        db.UserBudgetAllocations.Add(allocation);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.Equal(90m, budget!.TotalAllocatedPercent);
        Assert.Equal(10m, budget.UnallocatedPercent);
        Assert.Equal(500m, budget.UnallocatedAmount);
    }

    [Theory]
    [InlineData(30, 100)]
    [InlineData(45, 115)]
    public async Task Unallocated_WhenAllocationsTotal100PercentOrMore_IsAbsent(int housingPercent, int expectedTotal)
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation allocation = FullyAllocatedBudget();
        allocation.HousingAmount = housingPercent * 50m; // 1% of $5,000 income is $50
        db.UserBudgetAllocations.Add(allocation);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.Equal(expectedTotal, budget!.TotalAllocatedPercent);
        Assert.Null(budget.UnallocatedPercent);
        Assert.Null(budget.UnallocatedAmount);
    }

    [Fact]
    public async Task Budget_UsesTheMostRecentPeriod_RegardlessOfInsertionOrder()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation october = FullyAllocatedBudget(period: "2026-10");
        october.MonthlyIncome = 6_000m;
        october.HousingAmount = 1_800m; // amounts stay fixed when income changes
        UserBudgetAllocation lastYear = FullyAllocatedBudget(period: "2025-12");
        lastYear.MonthlyIncome = 9_000m;
        // Newest period inserted first, so the latest Id belongs to an older period.
        db.UserBudgetAllocations.AddRange(october, FullyAllocatedBudget(period: "2026-09"), lastYear);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.Equal("2026-10", budget!.Period);
        Assert.Equal(6_000m, budget.MonthlyIncome);
        Assert.Equal(1_800m, budget.Allocations.Single(a => a.Name == "Housing").Amount);
    }

    [Fact]
    public async Task Budget_IgnoresOtherUsersAndSoftDeletedAllocations()
    {
        await using AppDbContext db = CreateContext();
        UserBudgetAllocation mine = FullyAllocatedBudget(period: "2026-08");
        UserBudgetAllocation otherUsers = FullyAllocatedBudget(OtherUserId, "2026-12");
        UserBudgetAllocation deleted = FullyAllocatedBudget(period: "2026-11");
        deleted.IsDeleted = true;
        db.UserBudgetAllocations.AddRange(mine, otherUsers, deleted);
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        Assert.Equal("2026-08", budget!.Period);
        Assert.Null(await GetBudgetAsync(db, "user-without-budget"));
    }

    [Theory]
    [InlineData("2026-10", "October 2026")]
    [InlineData("2025-01", "January 2025")]
    [InlineData("Q4 plan", "Q4 plan")]
    [InlineData(null, "")]
    public void PeriodLabel_FormatsStoredPeriodsAsMonthNames_AndKeepsUnrecognisedValues(string? period,
        string expected)
    {
        Assert.Equal(expected, DashboardBudgetFormat.PeriodLabel(period));
    }

    [Theory]
    [InlineData("30", "30%")]
    [InlineData("12.5", "12.5%")]
    [InlineData("33.333", "33.3%")]
    [InlineData("0", "0%")]
    public void Percent_RoundsToOneDecimalPlaceOnlyForDisplay(string percent, string expected)
    {
        Assert.Equal(expected, DashboardBudgetFormat.Percent(decimal.Parse(percent, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Allocations_CarryAKeyMatchingTheCssColourTokenNames()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetAllocations.Add(FullyAllocatedBudget());
        await db.SaveChangesAsync();

        BudgetSummaryDto? budget = await GetBudgetAsync(db);

        var keys = budget!.Allocations.ToDictionary(a => a.Name, a => a.Key);
        Assert.Equal("debt-repayment", keys["Debt Repayment"]);
        Assert.Equal("housing", keys["Housing"]);
        Assert.All(budget.Allocations, a => Assert.Matches("^[a-z-]+$", a.Key));
    }
}
