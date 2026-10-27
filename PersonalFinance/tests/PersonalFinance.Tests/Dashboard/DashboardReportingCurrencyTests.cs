using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// The Dashboard's "now" figures in the Reporting Currency (ADR 0016): goals in other currencies convert at the latest
/// rate and their totals are summed once; the Net Worth projection converts its inputs once, then projects in the
/// Reporting Currency; without any rate the figures stay in their own currency and the caption says so.
/// </summary>
public class DashboardReportingCurrencyTests
{
    private const string UserId = "dashboard-currency-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GoalsOverview_ConvertsEachGoalAtTheLatestRate_AndSumsOnce()
    {
        await using AppDbContext db = CreateContext(Rate(25_800m));
        db.SavingsGoals.AddRange(
            new SavingsGoal { Id = 1, UserId = UserId, Name = "Car", TargetAmount = 200m, CurrentAmount = 100m },
            new SavingsGoal
            {
                Id = 2, UserId = UserId, Name = "Tết", CurrencyCode = "VND", TargetAmount = 5_160_000m, CurrentAmount = 2_580_000m
            });
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await SummaryAsync(db);

        Assert.Equal("USD", summary.ReportingCurrency);
        Assert.Equal((400m, 200m, 50m), (summary.GoalsOverview.TotalTarget, summary.GoalsOverview.TotalSaved, summary.GoalsOverview.FundedPercent));
        SavingsGoalSummaryDto tet = Assert.Single(summary.SavingsGoals, g => g.Name == "Tết");
        Assert.Equal(("VND", 2_580_000m, 100m, 200m), (tet.CurrencyCode, tet.CurrentAmount, tet.ReportingCurrentAmount, tet.ReportingTargetAmount));
        Assert.NotNull(summary.Conversion);
        Assert.True(summary.Conversion.Converted);
        Assert.Equal("VND", Assert.Single(summary.Conversion.Rates).QuoteCurrency);
    }

    [Fact]
    public async Task NetWorth_ConvertsItsInputsOnce_ThenProjectsInTheReportingCurrency()
    {
        await using AppDbContext db = CreateContext(Rate(25_800m));
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "vi", ReportingCurrency = "VND" });
        db.NetWorthParameters.Add(Parameters());
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await SummaryAsync(db);

        Assert.Equal("VND", summary.ReportingCurrency);
        Assert.NotNull(summary.NetWorth);
        Assert.Equal("VND", summary.NetWorth.CurrencyCode);
        Assert.Equal(25_800_000m, summary.NetWorth.CurrentNetWorth);
        Assert.Equal(500m * 25m * 25_800m, summary.NetWorth.FiTarget);
        Assert.Equal(2_580_000m, summary.NetWorth.AnnualSavings);
    }

    [Fact]
    public async Task WithNoRate_FiguresStayInTheirOwnCurrency_AndTheCaptionSaysUnavailable()
    {
        await using AppDbContext db = CreateContext();
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "vi", ReportingCurrency = "VND" });
        db.NetWorthParameters.Add(Parameters());
        await db.SaveChangesAsync();

        DashboardSummaryDto summary = await SummaryAsync(db);

        Assert.NotNull(summary.NetWorth);
        Assert.Equal(("USD", 1_000m), (summary.NetWorth.CurrencyCode, summary.NetWorth.CurrentNetWorth));
        Assert.NotNull(summary.Conversion);
        Assert.True(summary.Conversion.Unavailable);
    }

    private static NetWorthParameters Parameters() => new()
    {
        UserId = UserId,
        StartingBalance = 1_000m,
        AnnualSavings = 100m,
        AnnualIncome = 1_000m,
        AnnualSpending = 500m,
        CurrentAge = 30,
        RetirementAge = 31,
        BaseRoi = 0m,
        ConservativeRoi = 0m,
        AggressiveRoi = 0m,
        AggressiveSavings = 100m
    };

    private static Task<DashboardSummaryDto> SummaryAsync(AppDbContext db) =>
        new DashboardService(db, TestBudgetDataProviderFactory.Create(db), TestCurrencyConverter.Create(db, new FixedTimeProvider(Now)))
            .GetDashboardSummaryAsync(UserId);

    private static AppDbContext CreateContext(params ExchangeRate[] rates)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DashboardCurrency_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.ExchangeRates.AddRange(rates);
        db.SaveChanges();
        return db;
    }

    private static ExchangeRate Rate(decimal rate) => new()
    {
        BaseCurrency = "USD", QuoteCurrency = "VND", Date = new DateOnly(2026, 10, 26), Rate = rate, Source = "test"
    };
}
