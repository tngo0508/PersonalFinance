using System.Globalization;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.FinancialUniverse;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The labels the page puts in front of the reader and into the JSON island: localized kind names, the user's own
/// names untouched, amounts in the Reporting Currency, sections in rank order, Placeholders in the section they stand
/// in for, and every key present in en.json.
/// </summary>
public class FinancialUniverseTextTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void EveryKeyTheBuilderReads_IsInEnglish()
    {
        List<string> keys =
        [
            .. FinancialUniverseText.Keys,
            "FinancialUniverse.Title", "FinancialUniverse.Lead", "FinancialUniverse.Derived",
            "FinancialUniverse.Message.Unavailable", "Shell.Nav.FinancialUniverse",
            "FinancialUniverse.Month.Label", "FinancialUniverse.Month.Show", "FinancialUniverse.Row.Details",
            "FinancialUniverse.Panel.Title", "FinancialUniverse.Panel.Amount", "FinancialUniverse.Panel.Native",
            "FinancialUniverse.Panel.Period", "FinancialUniverse.Panel.Feature"
        ];
        Assert.Contains("FinancialUniverse.Kind.Shortfall", FinancialUniverseText.Keys);
        Assert.Contains("Shell.Nav.InvestmentCalculator", FinancialUniverseText.Keys);
        List<string> missing = keys.Where(key => !TestLocalization.Catalog.TryGet("en", key, out string _)).ToList();

        Assert.True(missing.Count == 0, "en.json has no " + string.Join(", ", missing));
    }

    [Fact]
    public void Build_GroupsNodesIntoSectionsInRankOrder_AndLabelsThem()
    {
        FinancialUniverseViewModel model;
        using (new RequestCultureScope("en-US"))
        {
            model = FinancialUniverseText.Build(SampleGraph(), TestLocalization.Localizer, EnUs);
        }

        Assert.Equal(
        [
            "Income sources", "Income", "Expense categories", "Savings or shortfall", "Savings Goals", "Net Worth", "Investment Scenarios"
        ], model.Sections.Select(s => s.Title));
        Assert.Equal(["income-sources", "income", "expense-categories", "savings", "savings-goals", "net-worth", "investment-scenarios"],
            model.Sections.Select(s => s.Key));
        Assert.Equal("October 2026", model.MonthLabel);

        FinancialUniverseNodeViewModel salary = Assert.Single(model.Sections[0].Rows);
        Assert.Equal(("Salary", "$3,000", "Open Budget Tracker", false), (salary.Label, salary.AmountText, salary.LinkText, salary.IsPlaceholder));
        Assert.Equal(("Income", "$3,000"), (model.Sections[1].Rows[0].Label, model.Sections[1].Rows[0].AmountText));
        Assert.Equal(["Rent", "Other"], model.Sections[2].Rows.Select(r => r.Label));
        Assert.Equal((0, (int?)null), (model.Sections[2].Rows[0].ColorSlot, model.Sections[2].Rows[1].ColorSlot));
        Assert.Equal(("Savings", "$1,000", "Open Analysis"), (model.Sections[3].Rows[0].Label, model.Sections[3].Rows[0].AmountText, model.Sections[3].Rows[0].LinkText));

        FinancialUniverseNodeViewModel goal = Assert.Single(model.Sections[4].Rows);
        Assert.Equal(("Tết", "$150", "of $1,000", "Open Savings Goals"), (goal.Label, goal.AmountText, goal.DetailText, goal.LinkText));
        FinancialUniverseNodeViewModel netWorth = Assert.Single(model.Sections[5].Rows);
        Assert.Equal(("Net Worth", "$50,000", "$12,000 saved a year"), (netWorth.Label, netWorth.AmountText, netWorth.DetailText));
        FinancialUniverseNodeViewModel scenario = Assert.Single(model.Sections[6].Rows);
        Assert.Equal(("No Investment Scenarios saved yet.", "", "Open Investment Calculator", true),
            (scenario.Label, scenario.AmountText, scenario.LinkText, scenario.IsPlaceholder));
        Assert.Equal("$1,000", Assert.Single(model.Edges, e => e.Target == "savings").WeightText);
        Assert.Equal("", Assert.Single(model.Edges, e => e.Target == "net-worth").WeightText);
    }

    [Fact]
    public void Build_InVietnamese_LocalizesTheAppsNodesButNotTheUsersNames()
    {
        FinancialUniverseViewModel model;
        using (new RequestCultureScope("vi-VN"))
        {
            model = FinancialUniverseText.Build(SampleGraph(), TestLocalization.Localizer, CultureInfo.GetCultureInfo("vi-VN"));
        }

        Assert.Equal("Salary", model.Sections[0].Rows[0].Label);
        Assert.Equal("Tết", model.Sections[4].Rows[0].Label);
        // The Vietnamese is drafted by the i18n workflow on the PR; until then English falls through, never the key.
        Assert.All(model.Nodes, n => Assert.DoesNotContain("FinancialUniverse.", n.Label, StringComparison.Ordinal));
        Assert.All(model.Sections, s => Assert.DoesNotContain("FinancialUniverse.", s.Title, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WhenNoRateConvertedAnAmount_ShowsTheNativeAmount()
    {
        var graph = new FinancialUniverseGraphDto
        {
            Year = 2026,
            Month = 10,
            CurrencyCode = "USD",
            Nodes =
            [
                new FinancialUniverseNodeDto
                {
                    Id = "net-worth", Kind = FinancialUniverseNodeKind.NetWorth, CurrencyCode = "USD", Amount = null,
                    NativeAmount = 26_000_000m, NativeCurrencyCode = "VND", LinkTarget = FinancialUniverseLinkTarget.NetWorth
                }
            ]
        };

        FinancialUniverseViewModel model = FinancialUniverseText.Build(graph, TestLocalization.Localizer, EnUs);

        Assert.Equal("₫26,000,000", model.Nodes[0].AmountText);
        Assert.Null(model.Nodes[0].Amount);
        // The panel's own-currency line would repeat the amount, so it stays empty.
        Assert.Equal("", model.Nodes[0].NativeAmountText);
    }

    [Fact]
    public void Build_WritesThePanelsLines_KindPeriodNativeAmountAndFeature()
    {
        FinancialUniverseGraphDto graph = SampleGraph();
        FinancialUniverseNodeDto goal = graph.Nodes.Single(n => n.Id == "goal-2");
        goal.NativeAmount = 3_900_000m;
        goal.NativeCurrencyCode = "VND";
        FinancialUniverseNodeDto netWorth = graph.Nodes.Single(n => n.Id == "net-worth");
        netWorth.NativeAmount = 50_000m;
        netWorth.NativeCurrencyCode = "USD";

        FinancialUniverseViewModel model;
        using (new RequestCultureScope("en-US"))
        {
            model = FinancialUniverseText.Build(graph, TestLocalization.Localizer, EnUs);
        }

        FinancialUniverseNodeViewModel rent = model.Nodes.Single(n => n.Id == "expense-0");
        Assert.Equal(("Expense category", "The figure for October 2026.", "Budget Tracker", ""),
            (rent.KindText, rent.PeriodText, rent.FeatureText, rent.NativeAmountText));
        FinancialUniverseNodeViewModel savings = model.Nodes.Single(n => n.Id == "savings");
        Assert.Equal(("Savings", "The figure for October 2026.", "Analysis"), (savings.KindText, savings.PeriodText, savings.FeatureText));
        // A goal's balance, Net Worth and a scenario are not month-bound; a stored amount in another currency is shown.
        FinancialUniverseNodeViewModel tet = model.Nodes.Single(n => n.Id == "goal-2");
        Assert.Equal(("Savings Goal", "Not tied to a month: the latest saved figure.", "Savings Goals", "₫3,900,000"),
            (tet.KindText, tet.PeriodText, tet.FeatureText, tet.NativeAmountText));
        FinancialUniverseNodeViewModel worth = model.Nodes.Single(n => n.Id == "net-worth");
        Assert.Equal(("Net Worth", "Not tied to a month: the latest saved figure.", "Net Worth", ""),
            (worth.KindText, worth.PeriodText, worth.FeatureText, worth.NativeAmountText));
        FinancialUniverseNodeViewModel placeholder = model.Nodes.Single(n => n.IsPlaceholder);
        Assert.Equal(("Nothing here yet", "", "Investment Calculator", "Open Investment Calculator"),
            (placeholder.KindText, placeholder.PeriodText, placeholder.FeatureText, placeholder.LinkText));
    }

    [Fact]
    public void Build_ListsTheMonthOptions_NewestFirst_WithTheGraphsMonthSelected()
    {
        FinancialUniverseGraphDto graph = SampleGraph();
        graph.AvailableMonths =
        [
            new FinancialUniverseMonthDto { Year = 2026, Month = 11 },
            new FinancialUniverseMonthDto { Year = 2026, Month = 10 },
            new FinancialUniverseMonthDto { Year = 2025, Month = 12 }
        ];

        FinancialUniverseViewModel model = FinancialUniverseText.Build(graph, TestLocalization.Localizer, EnUs);

        Assert.Equal(["2026-11", "2026-10", "2025-12"], model.AvailableMonths.Select(m => m.Value));
        Assert.Equal(["November 2026", "October 2026", "December 2025"], model.AvailableMonths.Select(m => m.Label));
        Assert.Equal("2026-10", Assert.Single(model.AvailableMonths, m => m.IsSelected).Value);
    }

    [Fact]
    public void Build_WhenTheListLacksTheGraphsMonth_AddsItSelected()
    {
        FinancialUniverseGraphDto graph = SampleGraph();
        graph.AvailableMonths = [new FinancialUniverseMonthDto { Year = 2026, Month = 11 }, new FinancialUniverseMonthDto { Year = 2026, Month = 9 }];

        FinancialUniverseViewModel model = FinancialUniverseText.Build(graph, TestLocalization.Localizer, EnUs);

        Assert.Equal(["2026-11", "2026-10", "2026-09"], model.AvailableMonths.Select(m => m.Value));
        Assert.Equal("2026-10", Assert.Single(model.AvailableMonths, m => m.IsSelected).Value);
    }

    [Theory]
    [InlineData("2026-10", true, 2026, 10)]
    [InlineData("2000-01", true, 2000, 1)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("2026-1", false, 0, 0)]
    [InlineData("October 2026", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void TryParsePeriod_ReadsThePickersValueOnly(string? period, bool expected, int year, int month)
    {
        Assert.Equal(expected, FinancialUniverseText.TryParsePeriod(period, out int parsedYear, out int parsedMonth));
        Assert.Equal((year, month), (parsedYear, parsedMonth));
    }

    [Fact]
    public void PageData_CarriesLabelsAndHrefs_SoTheScriptComposesNothing()
    {
        FinancialUniverseViewModel model;
        using (new RequestCultureScope("en-US"))
        {
            model = FinancialUniverseText.Build(SampleGraph(), TestLocalization.Localizer, EnUs);
        }

        FinancialUniversePageData data = FinancialUniversePageData.From(model, target => "/" + FinancialUniverseLinks.Route(target).Controller);

        Assert.Equal(model.Nodes.Count, data.Nodes.Count);
        FinancialUniversePageNode goal = Assert.Single(data.Nodes, n => n.Id == "goal-2");
        Assert.Equal(("SavingsGoal", "Tết", "$150", "of $1,000", "/SavingsGoals", 150m), (goal.Kind, goal.Label, goal.AmountText, goal.DetailText, goal.Href, goal.Amount));
        // The detail panel's lines travel in the island too, so the script composes none of them.
        Assert.Equal(("Savings Goal", "Not tied to a month: the latest saved figure.", "Savings Goals", "Open Savings Goals"),
            (goal.KindText, goal.PeriodText, goal.FeatureText, goal.LinkText));
        Assert.Equal("/InvestmentCalculator", Assert.Single(data.Nodes, n => n.IsPlaceholder).Href);
        Assert.Same(model.Edges, data.Edges);
    }

    [Fact]
    public void Links_RouteEveryTargetToAnExistingController()
    {
        foreach (FinancialUniverseLinkTarget target in Enum.GetValues<FinancialUniverseLinkTarget>())
        {
            (string controller, string action) = FinancialUniverseLinks.Route(target);
            Type? type = typeof(FinancialUniverseLinks).Assembly.GetType($"PersonalFinance.Web.Controllers.{controller}Controller");
            Assert.NotNull(type);
            Assert.NotNull(type.GetMethod(action));
        }
    }

    private static FinancialUniverseGraphDto SampleGraph() => new()
    {
        Year = 2026,
        Month = 10,
        CurrencyCode = "USD",
        Nodes =
        [
            Node("income-source-0", FinancialUniverseNodeKind.IncomeSource, "Salary", 3_000m, FinancialUniverseLinkTarget.BudgetTracker),
            Node("income", FinancialUniverseNodeKind.Income, null, 3_000m, FinancialUniverseLinkTarget.BudgetTracker),
            Node("expense-0", FinancialUniverseNodeKind.ExpenseCategory, "Rent", 1_500m, FinancialUniverseLinkTarget.BudgetTracker, colorSlot: 0),
            Node("other", FinancialUniverseNodeKind.Other, null, 500m, FinancialUniverseLinkTarget.BudgetTracker),
            Node("savings", FinancialUniverseNodeKind.Savings, null, 1_000m, FinancialUniverseLinkTarget.Analysis),
            Node("goal-2", FinancialUniverseNodeKind.SavingsGoal, "Tết", 150m, FinancialUniverseLinkTarget.SavingsGoals, secondary: 1_000m),
            Node("net-worth", FinancialUniverseNodeKind.NetWorth, null, 50_000m, FinancialUniverseLinkTarget.NetWorth, secondary: 12_000m),
            new FinancialUniverseNodeDto
            {
                Id = "placeholder-investmentscenario", Kind = FinancialUniverseNodeKind.Placeholder, CurrencyCode = "USD",
                LinkTarget = FinancialUniverseLinkTarget.InvestmentCalculator, PlaceholderFor = FinancialUniverseNodeKind.InvestmentScenario
            }
        ],
        Edges =
        [
            new FinancialUniverseEdgeDto { SourceId = "income", TargetId = "savings", Weight = 1_000m },
            new FinancialUniverseEdgeDto { SourceId = "savings", TargetId = "net-worth", Weight = null }
        ]
    };

    private static FinancialUniverseNodeDto Node(string id, FinancialUniverseNodeKind kind, string? name, decimal amount,
        FinancialUniverseLinkTarget target, decimal? secondary = null, int? colorSlot = null) => new()
    {
        Id = id, Kind = kind, Name = name, Amount = amount, SecondaryAmount = secondary, CurrencyCode = "USD", LinkTarget = target, ColorSlot = colorSlot
    };
}
