using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.FinancialUniverse;
using PersonalFinance.ApiService.Features.InvestmentScenarios;
using PersonalFinance.ApiService.Features.NetWorth;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Analysis;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The Financial Universe projection (spec #617, slice 1): income sources pool into Income, small and surplus expense
/// categories fold into Other, Savings or Shortfall sits between, goals weigh the month's Contributions, Net Worth and
/// the scenarios are relationships, every empty kind gets one Placeholder, and every amount is in the Reporting
/// Currency. Each other Feature is a fake behind its interface; the currency converter is real, over InMemory rates.
/// </summary>
public class FinancialUniverseServiceTests
{
    private const string UserId = "universe-user";
    private const int Year = 2026;
    private const int Month = 10;
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmptyAccount_HasOnePlaceholderPerKind_JoinedByRelationships()
    {
        await using AppDbContext db = CreateContext();
        var world = new World();

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        Assert.All(graph.Nodes, n => Assert.Equal(FinancialUniverseNodeKind.Placeholder, n.Kind));
        Assert.Equal(
        [
            FinancialUniverseNodeKind.IncomeSource, FinancialUniverseNodeKind.ExpenseCategory,
            FinancialUniverseNodeKind.SavingsGoal, FinancialUniverseNodeKind.NetWorth, FinancialUniverseNodeKind.InvestmentScenario
        ], graph.Nodes.Select(n => n.PlaceholderFor!.Value));
        Assert.Equal(
        [
            FinancialUniverseLinkTarget.BudgetTracker, FinancialUniverseLinkTarget.BudgetTracker,
            FinancialUniverseLinkTarget.SavingsGoals, FinancialUniverseLinkTarget.NetWorth,
            FinancialUniverseLinkTarget.InvestmentCalculator
        ], graph.Nodes.Select(n => n.LinkTarget));
        // With no Savings node, only Net Worth's relationship to the scenarios remains.
        FinancialUniverseEdgeDto edge = Assert.Single(graph.Edges);
        Assert.Equal(("placeholder-networth", "placeholder-investmentscenario", (decimal?)null), (edge.SourceId, edge.TargetId, edge.Weight));
        Assert.Equal("USD", graph.CurrencyCode);
        Assert.Equal((Year, Month), (graph.Year, graph.Month));
    }

    [Fact]
    public async Task IncomeSources_PoolIntoOneIncomeNode_LargestFirst()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 5_000m, actualExpenses: 0m,
                Income("Side gig", 2_000m), Income("Salary", 3_000m))
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        List<FinancialUniverseNodeDto> sources = graph.Nodes.Where(n => n.Kind == FinancialUniverseNodeKind.IncomeSource).ToList();
        Assert.Equal(["Salary", "Side gig"], sources.Select(s => s.Name));
        Assert.Equal(["income-source-0", "income-source-1"], sources.Select(s => s.Id));
        FinancialUniverseNodeDto income = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Income);
        Assert.Equal(5_000m, income.Amount);
        Assert.Equal(3_000m, Assert.Single(graph.Edges, e => e.SourceId == "income-source-0").Weight);
        Assert.Equal("income", Assert.Single(graph.Edges, e => e.SourceId == "income-source-1").TargetId);
        Assert.DoesNotContain(graph.Nodes, n => n.PlaceholderFor == FinancialUniverseNodeKind.IncomeSource);
        // Everything earned is saved: Savings takes the whole income.
        Assert.Equal(5_000m, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Savings).Amount);
    }

    [Fact]
    public async Task ExpenseCategories_FoldSmallSurplusAndUncategorizedSpendingIntoOther()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            // 9,000 spent of which 8,450 is categorized; Tea is under 3% of the 10,000 flow and Books is the sixth category.
            Month = BudgetMonth(actualIncome: 10_000m, actualExpenses: 9_000m,
                Expense("Tea", 200m), Expense("Books", 350m), Expense("Gym", 400m), Expense("Fun", 500m),
                Expense("Car", 1_000m), Expense("Food", 2_000m), Expense("Rent", 4_000m))
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        List<FinancialUniverseNodeDto> categories = graph.Nodes.Where(n => n.Kind == FinancialUniverseNodeKind.ExpenseCategory).ToList();
        Assert.Equal(["Rent", "Food", "Car", "Fun", "Gym"], categories.Select(c => c.Name));
        Assert.Equal([0, 1, 2, 3, 4], categories.Select(c => c.ColorSlot!.Value));
        Assert.Equal(["expense-0", "expense-1", "expense-2", "expense-3", "expense-4"], categories.Select(c => c.Id));
        FinancialUniverseNodeDto other = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Other);
        Assert.Equal(350m + 200m + 550m, other.Amount);
        Assert.Equal(1_000m, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Savings).Amount);
        Assert.Equal(4_000m, Assert.Single(graph.Edges, e => e.TargetId == "expense-0").Weight);
        Assert.Equal(1_100m, Assert.Single(graph.Edges, e => e.TargetId == "other").Weight);
        Assert.Equal(1_000m, Assert.Single(graph.Edges, e => e.TargetId == "savings").Weight);
        Assert.All(graph.Edges.Where(e => e.SourceId == "income"), e => Assert.Equal("income", e.SourceId));
        Assert.DoesNotContain(graph.Nodes, n => n.PlaceholderFor == FinancialUniverseNodeKind.ExpenseCategory);
    }

    [Fact]
    public async Task ExpenseCategories_FoldIntoOtherExactlyAsMoneyFlowDoes()
    {
        // The same month through both Features (#621): Money Flow's chart and the Financial Universe graph must keep
        // the same categories, in the same colour slots, and fold the same ones into Other. Spending is fully
        // categorized, so neither has an Uncategorized remainder to account for.
        (string Name, decimal Actual)[] spending =
        [
            ("Tea", 200m), ("Books", 350m), ("Gym", 400m), ("Fun", 500m), ("Car", 1_000m), ("Food", 2_000m), ("Rent", 4_000m)
        ];
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 10_000m, actualExpenses: 8_450m, spending.Select(c => Expense(c.Name, c.Actual)).ToArray())
        };
        var insights = new CategoryInsightsDto
        {
            Year = Year,
            MonthNumber = Month,
            ActualIncome = 10_000m,
            ActualExpenses = 8_450m,
            Categories = spending.Select(c => new CategoryBreakdownDto { CategoryName = c.Name, ActualAmount = c.Actual }).ToList()
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);
        MoneyFlowViewModel? flow;
        using (new RequestCultureScope("en-US"))
        {
            flow = MoneyFlowBuilder.Build(insights, TestLocalization.Localizer);
        }

        Assert.NotNull(flow);
        List<(string? Name, int? Slot)> universeKept = graph.Nodes
            .Where(n => n.Kind == FinancialUniverseNodeKind.ExpenseCategory)
            .Select(n => (n.Name, n.ColorSlot))
            .ToList();
        List<(string? Name, int? Slot)> flowKept = flow.Nodes
            .Where(n => n.Kind == MoneyFlowNodeKind.Category)
            .Select(n => ((string?)n.Label, n.ColorSlot))
            .ToList();
        Assert.Equal(flowKept, universeKept);
        Assert.Equal(["Rent", "Food", "Car", "Fun", "Gym"], universeKept.Select(k => k.Name));
        Assert.Equal(flow.Nodes.Single(n => n.Kind == MoneyFlowNodeKind.Other).Amount,
            Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Other).Amount);
        Assert.Equal(["Books", "Tea"], flow.TableRows.Where(r => r.InOther).Select(r => r.Name));
    }

    [Fact]
    public async Task SpendingWithNoCategories_ShowsAsOther_NotAsNothingSpent()
    {
        await using AppDbContext db = CreateContext();
        var world = new World { Month = BudgetMonth(actualIncome: 3_000m, actualExpenses: 1_200m) };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        Assert.Equal(1_200m, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Other).Amount);
        Assert.DoesNotContain(graph.Nodes, n => n.PlaceholderFor == FinancialUniverseNodeKind.ExpenseCategory);
        Assert.Equal(1_800m, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Savings).Amount);
    }

    [Fact]
    public async Task IncomeWithNoSpending_ShowsTheSpendingPlaceholder_HangingOffIncome()
    {
        await using AppDbContext db = CreateContext();
        var world = new World { Month = BudgetMonth(actualIncome: 3_000m, actualExpenses: 0m, Income("Salary", 3_000m)) };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto placeholder = Assert.Single(graph.Nodes, n => n.PlaceholderFor == FinancialUniverseNodeKind.ExpenseCategory);
        Assert.Equal("income", Assert.Single(graph.Edges, e => e.TargetId == placeholder.Id).SourceId);
        Assert.DoesNotContain(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Other);
    }

    [Fact]
    public async Task ShortfallMonth_HasAShortfallInsteadOfSavings_AndTheGoalsHangOffIt()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 1_000m, actualExpenses: 1_500m, Expense("Rent", 1_500m)),
            Goals = [Goal(1, "Car", 100m, 1_000m)]
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto shortfall = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Shortfall);
        Assert.Equal((500m, FinancialUniverseLinkTarget.Analysis), (shortfall.Amount, shortfall.LinkTarget));
        Assert.DoesNotContain(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Savings);
        Assert.Equal(500m, Assert.Single(graph.Edges, e => e.TargetId == "shortfall").Weight);
        Assert.Equal("shortfall", Assert.Single(graph.Edges, e => e.TargetId == "goal-1").SourceId);
        Assert.Equal("shortfall", Assert.Single(graph.Edges, e => e.TargetId == "placeholder-networth").SourceId);
    }

    [Fact]
    public async Task Goals_WeighTheMonthsContributions_AndAGoalWithNoneStillGetsAZeroEdge()
    {
        await using AppDbContext db = CreateContext();
        SavingsGoalResponseDto car = Goal(1, "Car", 150m, 1_000m);
        car.Contributions =
        [
            Contribution(100m, new DateOnly(2026, 10, 3)),
            Contribution(50m, new DateOnly(2026, 10, 20)),
            Contribution(999m, new DateOnly(2026, 9, 30))
        ];
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 2_000m, actualExpenses: 1_000m, Expense("Rent", 1_000m)),
            Goals = [car, Goal(2, "Tết", 20m, 300m)]
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto carNode = Assert.Single(graph.Nodes, n => n.Id == "goal-1");
        Assert.Equal((FinancialUniverseNodeKind.SavingsGoal, "Car", 150m, 1_000m, 1, FinancialUniverseLinkTarget.SavingsGoals),
            (carNode.Kind, carNode.Name, carNode.Amount, carNode.SecondaryAmount, carNode.SourceId, carNode.LinkTarget));
        Assert.Equal(150m, Assert.Single(graph.Edges, e => e.TargetId == "goal-1").Weight);
        Assert.Equal(0m, Assert.Single(graph.Edges, e => e.TargetId == "goal-2").Weight);
        Assert.DoesNotContain(graph.Nodes, n => n.PlaceholderFor == FinancialUniverseNodeKind.SavingsGoal);
    }

    [Fact]
    public async Task NetWorthAndScenarios_AreRelationshipsWithNoWeight()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 2_000m, actualExpenses: 1_000m, Expense("Rent", 1_000m)),
            NetWorth = new NetWorthParametersDto { CurrencyCode = "USD", StartingBalance = 50_000m, AnnualSavings = 12_000m },
            Scenarios = [Scenario(7, "Index fund", 1_000m, 200m), Scenario(3, "Bonds", 500m, 50m)]
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto netWorth = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.NetWorth);
        Assert.Equal(("net-worth", 50_000m, 12_000m, 50_000m, "USD"),
            (netWorth.Id, netWorth.Amount, netWorth.SecondaryAmount, netWorth.NativeAmount, netWorth.NativeCurrencyCode));
        Assert.Null(Assert.Single(graph.Edges, e => e.TargetId == "net-worth").Weight);
        Assert.Equal("savings", Assert.Single(graph.Edges, e => e.TargetId == "net-worth").SourceId);

        List<FinancialUniverseNodeDto> scenarios = graph.Nodes.Where(n => n.Kind == FinancialUniverseNodeKind.InvestmentScenario).ToList();
        Assert.Equal(["scenario-7", "scenario-3"], scenarios.Select(s => s.Id)); // the Feature's own order: last edited first
        Assert.Equal(("Index fund", 1_000m, 200m, 7), (scenarios[0].Name, scenarios[0].Amount, scenarios[0].SecondaryAmount, scenarios[0].SourceId));
        Assert.All(graph.Edges.Where(e => e.SourceId == "net-worth"), e => Assert.Null(e.Weight));
        Assert.Equal(2, graph.Edges.Count(e => e.SourceId == "net-worth"));
    }

    [Fact]
    public async Task Amounts_ConvertIntoTheReportingCurrency_ContributionsAtTheirOwnDay()
    {
        // USD is the Reporting Currency; the đồng rate rises from 25,000 to 26,000 on 20 October.
        await using AppDbContext db = CreateContext(Rate(25_000m, new DateOnly(2026, 10, 1)), Rate(26_000m, new DateOnly(2026, 10, 20)));
        SavingsGoalResponseDto tet = Goal(2, "Tết", 2_600_000m, 5_200_000m, "VND");
        tet.ReportingCurrentAmount = 100m;
        tet.ReportingTargetAmount = 200m;
        tet.Contributions = [Contribution(2_500_000m, new DateOnly(2026, 10, 5)), Contribution(2_600_000m, new DateOnly(2026, 10, 25))];
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 2_000m, actualExpenses: 1_000m, Expense("Rent", 1_000m)),
            Goals = [tet],
            NetWorth = new NetWorthParametersDto { CurrencyCode = "VND", StartingBalance = 26_000_000m, AnnualSavings = 2_600_000m },
            Scenarios = [Scenario(1, "Quỹ", 13_000_000m, 260_000m, "VND")]
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto goal = Assert.Single(graph.Nodes, n => n.Id == "goal-2");
        Assert.Equal((100m, 200m, 2_600_000m, "VND", "USD"), (goal.Amount, goal.SecondaryAmount, goal.NativeAmount, goal.NativeCurrencyCode, goal.CurrencyCode));
        // 2,500,000 at 25,000 on the 5th plus 2,600,000 at 26,000 on the 25th.
        Assert.Equal(200m, Assert.Single(graph.Edges, e => e.TargetId == "goal-2").Weight);
        FinancialUniverseNodeDto netWorth = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.NetWorth);
        Assert.Equal((1_000m, 100m), (netWorth.Amount, netWorth.SecondaryAmount));
        FinancialUniverseNodeDto scenario = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.InvestmentScenario);
        Assert.Equal((500m, 10m, 13_000_000m), (scenario.Amount, scenario.SecondaryAmount, scenario.NativeAmount));
        Assert.NotNull(graph.Conversion);
        Assert.True(graph.Conversion.Converted);
        Assert.Equal("VND", Assert.Single(graph.Conversion.Rates).QuoteCurrency);
    }

    [Fact]
    public async Task WithNoRate_AConvertedAmountIsNull_NeverZero()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            NetWorth = new NetWorthParametersDto { CurrencyCode = "VND", StartingBalance = 26_000_000m, AnnualSavings = 2_600_000m }
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        FinancialUniverseNodeDto netWorth = Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.NetWorth);
        Assert.Null(netWorth.Amount);
        Assert.Equal(26_000_000m, netWorth.NativeAmount);
        Assert.True(graph.Conversion!.Unavailable);
    }

    [Fact]
    public async Task Nodes_ComeInRankOrder()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 3_000m, actualExpenses: 1_000m, Income("Salary", 3_000m), Expense("Rent", 1_000m)),
            Goals = [Goal(1, "Car", 100m, 1_000m)],
            NetWorth = new NetWorthParametersDto { CurrencyCode = "USD", StartingBalance = 1m, AnnualSavings = 1m },
            Scenarios = [Scenario(1, "Fund", 1m, 1m)]
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        Assert.Equal(
        [
            FinancialUniverseNodeKind.IncomeSource, FinancialUniverseNodeKind.Income, FinancialUniverseNodeKind.ExpenseCategory,
            FinancialUniverseNodeKind.Savings, FinancialUniverseNodeKind.SavingsGoal, FinancialUniverseNodeKind.NetWorth,
            FinancialUniverseNodeKind.InvestmentScenario
        ], graph.Nodes.Select(n => n.Kind));
        Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(graph.Edges, e =>
        {
            Assert.Contains(graph.Nodes, n => n.Id == e.SourceId);
            Assert.Contains(graph.Nodes, n => n.Id == e.TargetId);
        });
    }

    [Fact]
    public async Task GoogleSheetSource_LinksBudgetNodesToAnalysis()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Source = BudgetDataSource.GoogleSheet,
            Month = BudgetMonth(actualIncome: 3_000m, actualExpenses: 1_000m, Expense("Rent", 1_000m))
        };

        FinancialUniverseGraphDto graph = await world.GraphAsync(db);

        Assert.Equal(BudgetDataSource.GoogleSheet, graph.BudgetSource);
        Assert.Equal(FinancialUniverseLinkTarget.Analysis, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.Income).LinkTarget);
        Assert.Equal(FinancialUniverseLinkTarget.Analysis, Assert.Single(graph.Nodes, n => n.Kind == FinancialUniverseNodeKind.ExpenseCategory).LinkTarget);
    }

    [Fact]
    public async Task AvailableMonths_ListTheSourcesMonthsTheCurrentAndTheRequested_NewestFirst_EachOnce()
    {
        await using AppDbContext db = CreateContext();
        var world = new World
        {
            Month = BudgetMonth(actualIncome: 3_000m, actualExpenses: 1_000m, Expense("Rent", 1_000m)),
            // Two Sheet snapshots in August count once; a 2025 month shows the years are walked.
            DataMonths = [(2026, 8, 2), (2025, 12, 1)]
        };

        // Asked for August 2026 while the clock says October 2026 (Now), with data in October, August and December 2025.
        FinancialUniverseGraphDto graph = await world.GraphAsync(db, 2026, 8);

        Assert.Equal([(2026, 10), (2026, 8), (2025, 12)], graph.AvailableMonths.Select(m => (m.Year, m.Month)));
    }

    [Fact]
    public async Task AvailableMonths_WithNoBudgetData_StillOfferTheRequestedAndTheCurrentMonth()
    {
        await using AppDbContext db = CreateContext();
        var world = new World();

        FinancialUniverseGraphDto graph = await world.GraphAsync(db, 2024, 2);

        Assert.Equal([(2026, 10), (2024, 2)], graph.AvailableMonths.Select(m => (m.Year, m.Month)));
        Assert.Equal((2024, 2), (graph.Year, graph.Month));
    }

    private static AppDbContext CreateContext(params ExchangeRate[] rates)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FinancialUniverse_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.ExchangeRates.AddRange(rates);
        db.SaveChanges();
        return db;
    }

    private static ExchangeRate Rate(decimal rate, DateOnly date) => new()
    {
        BaseCurrency = "USD", QuoteCurrency = "VND", Date = date, Rate = rate, Source = "test"
    };

    private static BudgetMonthData BudgetMonth(decimal actualIncome, decimal actualExpenses, params BudgetMonthCategoryData[] categories) => new()
    {
        Year = Year,
        Month = Month,
        CurrencyCode = "USD",
        ActualIncome = actualIncome,
        ActualExpenses = actualExpenses,
        Categories = categories.ToList()
    };

    private static BudgetMonthCategoryData Income(string name, decimal actual) =>
        new() { Name = name, Actual = actual, Kind = BudgetCategoryKind.Income };

    private static BudgetMonthCategoryData Expense(string name, decimal actual) =>
        new() { Name = name, Actual = actual, Kind = BudgetCategoryKind.Expense };

    private static SavingsGoalResponseDto Goal(int id, string name, decimal current, decimal target, string currency = "USD") => new()
    {
        Id = id,
        Name = name,
        CurrencyCode = currency,
        ReportingCurrency = "USD",
        CurrentAmount = current,
        TargetAmount = target,
        ReportingCurrentAmount = currency == "USD" ? current : null,
        ReportingTargetAmount = currency == "USD" ? target : null
    };

    private static ContributionDto Contribution(decimal amount, DateOnly date) =>
        new() { Amount = amount, Date = date, Kind = ContributionKind.Regular };

    private static InvestmentScenarioDto Scenario(int id, string name, decimal initial, decimal monthly, string currency = "USD") => new()
    {
        Id = id, Name = name, CurrencyCode = currency, InitialAmount = initial, MonthlyContribution = monthly
    };

    /// <summary>The other Features as the Universe sees them: one fake per interface, each holding what the test seeds.</summary>
    private sealed class World
    {
        public BudgetDataSource Source { get; init; } = BudgetDataSource.BudgetTracker;
        public BudgetMonthData? Month { get; init; }
        public List<SavingsGoalResponseDto> Goals { get; init; } = [];
        public NetWorthParametersDto? NetWorth { get; init; }
        public List<InvestmentScenarioDto> Scenarios { get; init; } = [];

        /// <summary>The months the budget source holds besides <see cref="Month"/>, as (year, month, snapshots in it).</summary>
        public List<(int Year, int Month, int Records)> DataMonths { get; init; } = [];

        public Task<FinancialUniverseGraphDto> GraphAsync(AppDbContext db, int year = Year, int month = FinancialUniverseServiceTests.Month) =>
            new FinancialUniverseService(new FakeBudgetProvider(this), new FakeGoals(this), new FakeNetWorth(this),
                    new FakeScenarios(this), TestCurrencyConverter.Create(db, new FixedTimeProvider(Now)), new FixedTimeProvider(Now))
                .GetGraphAsync(UserId, year, month);
    }

    private sealed class FakeBudgetProvider(World world) : IBudgetDataProvider
    {
        public Task<BudgetDataSource> GetActiveSourceAsync(string userId, CancellationToken ct = default) => Task.FromResult(world.Source);

        public Task<BudgetMonthData?> GetMonthAsync(string userId, int year, int month, CancellationToken ct = default) =>
            Task.FromResult(year == Year && month == Month ? world.Month : null);

        public Task<List<BudgetMonthData>> GetMonthsAsync(string userId, int year, CancellationToken ct = default) => throw new NotSupportedException();

        /// <summary>One record per snapshot, as the Sheet reader answers; the seeded month counts once.</summary>
        public Task<List<BudgetTrendRecordData>> GetTrendRecordsAsync(string userId, int year, CancellationToken ct = default) =>
            Task.FromResult(DataMonths()
                .Where(m => m.Year == year)
                .SelectMany(m => Enumerable.Repeat(new BudgetTrendRecordData { MonthNumber = m.Month }, m.Records))
                .ToList());

        public Task<List<BudgetTransactionData>> GetTransactionsAsync(string userId, DateOnly from, DateOnly to, int? take = null, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<List<int>> GetAvailableYearsAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(DataMonths().Select(m => m.Year).Distinct().OrderBy(y => y).ToList());

        private IEnumerable<(int Year, int Month, int Records)> DataMonths() =>
            world.DataMonths.Concat(world.Month is null ? [] : [(Year, Month, 1)]);
    }

    private sealed class FakeGoals(World world) : ISavingsGoalService
    {
        public Task<List<SavingsGoalResponseDto>> GetSavingsGoalsAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(world.Goals);
        public Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateSavingsGoalAsync(string userId, int id, UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ContributionDto>?> GetContributionsAsync(string userId, int goalId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContributionResult> AddContributionAsync(string userId, int goalId, SaveContributionRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContributionResult> UpdateContributionAsync(string userId, int goalId, int contributionId, SaveContributionRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContributionResult> DeleteContributionAsync(string userId, int goalId, int contributionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContributionResult> CorrectBalanceAsync(string userId, int goalId, CorrectBalanceRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeNetWorth(World world) : INetWorthParametersService
    {
        public Task<NetWorthParametersDto?> LoadAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(world.NetWorth);
        public Task<NetWorthParametersDto> SaveAsync(string userId, NetWorthProjectionRequestDto parameters, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeScenarios(World world) : IInvestmentScenarioService
    {
        public Task<List<InvestmentScenarioDto>> ListAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(world.Scenarios);
        public Task<InvestmentScenarioDto?> GetAsync(string userId, int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentScenarioDto> CreateAsync(string userId, SaveInvestmentScenarioRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentScenarioDto?> UpdateAsync(string userId, int id, SaveInvestmentScenarioRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentScenarioDto?> DuplicateAsync(string userId, int id, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
