using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Analysis;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// The Money Flow model (GLOSSARY: Money Flow, Shortfall): one month of actual income moving into spending
/// categories and savings, drawn as a Sankey on the Analysis page.
/// </summary>
public sealed class MoneyFlowBuilderTests : IDisposable
{
    // The labels and month names follow the request culture; these tests read them in English.
    private readonly RequestCultureScope _culture = new("en-US");

    public void Dispose() => _culture.Dispose();

    private static CategoryInsightsDto Month(decimal income, decimal expenses,
        params (string Name, decimal Actual)[] categories)
    {
        return new CategoryInsightsDto
        {
            Year = 2026,
            MonthNumber = 9,
            MonthName = "September",
            ActualIncome = income,
            ActualExpenses = expenses,
            Categories = categories
                .Select(c => new CategoryBreakdownDto { CategoryName = c.Name, ActualAmount = c.Actual })
                .ToList()
        };
    }

    private static List<string> LinkLabels(MoneyFlowViewModel flow)
    {
        Dictionary<string, string> labels = flow.Nodes.ToDictionary(n => n.Key, n => n.Label);
        return flow.Links.Select(l => $"{labels[l.From]}>{labels[l.To]}:{l.Amount}").ToList();
    }

    [Fact]
    public void Build_WhenNoInsights_ReturnsNull()
    {
        Assert.Null(MoneyFlowBuilder.Build(null, TestLocalization.Localizer));
    }

    [Fact]
    public void Build_WhenNoIncomeAndNoExpenses_ReturnsNull()
    {
        Assert.Null(MoneyFlowBuilder.Build(Month(0m, 0m), TestLocalization.Localizer));
    }

    [Fact]
    public void Build_NormalMonth_FlowsIncomeIntoCategoriesAndSaved()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 700m, ("Rent", 500m), ("Food", 200m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal("September 2026", flow.MonthLabel);
        Assert.False(flow.HasShortfall);
        Assert.Equal(300m, flow.Saved);
        Assert.Equal(["Income>Rent:500", "Income>Food:200", "Income>Saved:300"], LinkLabels(flow));
        Assert.DoesNotContain(flow.Nodes, n => n.Kind == MoneyFlowNodeKind.Spending);
        Assert.Equal(1000m, flow.ShareBase);
    }

    [Fact]
    public void Build_ShortfallMonth_RoutesIncomeAndShortfallThroughSpending()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(3100m, 3420m, ("Rent", 1800m), ("Gifts", 1620m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.True(flow.HasShortfall);
        Assert.Equal(320m, flow.Shortfall);
        Assert.Equal(0m, flow.Saved);
        Assert.Equal(
            ["Income>Spending:3100", "Shortfall>Spending:320", "Spending>Rent:1800", "Spending>Gifts:1620"],
            LinkLabels(flow));
        Assert.DoesNotContain(flow.Nodes, n => n.Kind == MoneyFlowNodeKind.Saved);
        // Shares are of total spending, since income can't cover it.
        Assert.Equal(3420m, flow.ShareBase);
        Assert.Equal(52.6m, flow.TableRows.Single(r => r.Name == "Rent").SharePercent);
    }

    [Fact]
    public void Build_ExpensesWithoutIncome_HasOnlyShortfallIntoSpending()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(0m, 400m, ("Rent", 400m)), TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(["Shortfall>Spending:400", "Spending>Rent:400"], LinkLabels(flow));
        Assert.DoesNotContain(flow.Nodes, n => n.Kind == MoneyFlowNodeKind.Income);
    }

    [Fact]
    public void Build_IncomeWithoutExpenses_FlowsIncomeIntoSavedOnly()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(2000m, 0m), TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(["Income>Saved:2000"], LinkLabels(flow));
    }

    [Fact]
    public void Build_WhenCategoriesFallShortOfExpenses_AddsUncategorized()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 900m, ("Rent", 600m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(["Income>Rent:600", "Income>Uncategorized:300", "Income>Saved:100"], LinkLabels(flow));
        Assert.Contains(flow.TableRows, r => r.Name == "Uncategorized" && r.Amount == 300m);
    }

    [Fact]
    public void Build_WhenCategoriesExceedExpenses_UsesTheCategoryTotal()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 500m, ("Rent", 600m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(600m, flow.Expenses);
        Assert.Equal(400m, flow.Saved);
        Assert.DoesNotContain(flow.Nodes, n => n.Kind == MoneyFlowNodeKind.Uncategorized);
    }

    [Fact]
    public void Build_GroupsCategoriesUnderThreePercentOfTotalFlowIntoOther()
    {
        // Total flow 1000: 29.99 is under 3% and grouped; 30 is not.
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(
            Month(1000m, 559.99m, ("Rent", 500m), ("Phone", 30m), ("Books", 29.99m)), TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(
            ["Income>Rent:500", "Income>Phone:30", "Income>Other:29.99", "Income>Saved:440.01"],
            LinkLabels(flow));
        // The table lists every category, ungrouped.
        Assert.Contains(flow.TableRows, r => r.Name == "Books");
        Assert.DoesNotContain(flow.TableRows, r => r.Name == "Other");
    }

    [Fact]
    public void Build_ShortfallCountsTowardTheOtherThreshold()
    {
        // Total flow = income 900 + Shortfall 100 = 1000, so 3% is 30.
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(900m, 1000m, ("Rent", 970m), ("Books", 30m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Contains(flow.Nodes, n => n.Label == "Books");
        Assert.DoesNotContain(flow.Nodes, n => n.Kind == MoneyFlowNodeKind.Other);
    }

    [Fact]
    public void Build_IgnoresCategoriesWithNoPositiveActual()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(
            Month(1000m, 500m, ("Rent", 500m), ("Travel", 0m), ("Refunds", -20m)), TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(["Rent", "Saved"], flow.TableRows.Select(r => r.Name).ToList());
    }

    [Fact]
    public void Build_KeysNodesSoACategoryNamedLikeASpecialNodeStaysDistinct()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 600m, ("Saved", 600m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal(3, flow.Nodes.Select(n => n.Key).Distinct().Count());
        Assert.Equal(2, flow.Links.Count);
    }

    [Fact]
    public void Build_GivesEachCategoryItsOwnColourSlotInDisplayOrder()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 700m, ("Food", 200m), ("Rent", 500m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        Assert.Equal([0, 1],
            flow.Nodes.Where(n => n.Kind == MoneyFlowNodeKind.Category).Select(n => n.ColorSlot!.Value));
        Assert.All(flow.Nodes.Where(n => n.Kind != MoneyFlowNodeKind.Category), n => Assert.Null(n.ColorSlot));
        // The table shows the same swatch as the band.
        Assert.Equal(1, flow.TableRows.Single(r => r.Name == "Food").ColorSlot);
    }

    [Fact]
    public void Build_FoldsCategoriesPastTheColouredLimitIntoOther()
    {
        // Seven categories, each well above 3%: the five largest get colours, the last two join Other.
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(Month(1000m, 700m,
            ("A", 160m), ("B", 140m), ("C", 120m), ("D", 100m), ("E", 80m), ("F", 60m), ("G", 40m)),
            TestLocalization.Localizer);

        Assert.NotNull(flow);
        List<MoneyFlowNodeViewModel> categories = flow.Nodes.Where(n => n.Kind == MoneyFlowNodeKind.Category).ToList();
        Assert.Equal(MoneyFlowBuilder.MaxColoredCategories, categories.Count);
        Assert.Equal(100m, flow.Nodes.Single(n => n.Kind == MoneyFlowNodeKind.Other).Amount);
        MoneyFlowRowViewModel f = flow.TableRows.Single(r => r.Name == "F");
        Assert.True(f.InOther);
        Assert.Null(f.ColorSlot);
    }

    [Fact]
    public void Build_OrdersCategoriesByAmountThenSpecialNodesLast()
    {
        MoneyFlowViewModel? flow = MoneyFlowBuilder.Build(
            Month(1000m, 900m, ("Food", 200m), ("Rent", 500m), ("Fun", 10m)), TestLocalization.Localizer);

        Assert.NotNull(flow);
        List<string> rightHand = flow.Nodes.Where(n => n.Column > 0).Select(n => n.Label).ToList();
        Assert.Equal(["Rent", "Food", "Uncategorized", "Other", "Saved"], rightHand);
        Assert.Equal(["Rent", "Food", "Fun", "Uncategorized", "Saved"], flow.TableRows.Select(r => r.Name).ToList());
    }
}
