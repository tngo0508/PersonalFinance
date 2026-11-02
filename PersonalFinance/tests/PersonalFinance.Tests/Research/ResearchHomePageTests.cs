using System.Text.RegularExpressions;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Research;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Home page's workbench: the tracked-funds list grouped by role with its three-year figures, the
/// starting mixes checked against the Tracked Funds, the market backdrop from the macro data, and the view's shape.
/// </summary>
public class ResearchHomePageTests
{
    // Three funds: two bond funds in the configured order and one without a role, which comes last.
    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q2"],
        Funds =
        [
            new TrackedFundDto { Ticker = "CCC", Name = "Charlie Fund", Role = "Bonds" },
            new TrackedFundDto { Ticker = "AAA", Name = "Alpha Fund" },
            new TrackedFundDto { Ticker = "BBB", Name = "Bravo Fund", Role = "Bonds" },
            new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust", Role = "US broad" },
        ],
        Values =
        [
            new FundsPeriodDto
            {
                Period = "2026 Q2",
                Funds =
                [
                    new FundHoldingsDto { Ticker = "AAA", NetAssets = 5_000m },
                    new FundHoldingsDto { Ticker = "BBB", NetAssets = 2_000m },
                    new FundHoldingsDto { Ticker = "CCC", NetAssets = 3_000m },
                    new FundHoldingsDto { Ticker = "SPY", NetAssets = 9_000m },
                ],
            },
        ],
    };

    private static readonly FundReturnsSnapshotDto Returns = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Windows =
        [
            new FundReturnsWindowDto
            {
                Years = 1,
                Funds = [new FundWindowMetricsDto { Ticker = "CCC", AnnualizedReturn = 1m, Volatility = 1m }],
            },
            new FundReturnsWindowDto
            {
                Years = 3,
                Funds =
                [
                    new FundWindowMetricsDto { Ticker = "CCC", AnnualizedReturn = 4.5m, Volatility = 6m },
                    new FundWindowMetricsDto { Ticker = "BBB", AnnualizedReturn = 3.2m, Volatility = 5m },
                    new FundWindowMetricsDto { Ticker = "SPY", AnnualizedReturn = 12.1m, Volatility = 15m },
                ],
            },
        ],
    };

    private static readonly FundFactsSnapshotDto Facts = new()
    {
        Funds = [new FundFactsDto { Ticker = "BBB", ExpenseRatio = 0.05m }, new FundFactsDto { Ticker = "SPY", ExpenseRatio = 0.09m }],
    };

    private static ResearchHomeViewModel Build(FundsSnapshotDto? funds = null, FundReturnsSnapshotDto? returns = null,
        FundFactsSnapshotDto? facts = null, MacroSnapshotDto? macro = null, ResearchStatusDto? status = null) =>
        new(TestLocalization.Localizer, status, funds, returns, facts, macro);

    [Fact]
    public void FundGroups_GroupTheFundsByRole_InConfiguredOrder_WithTheThreeYearFigures()
    {
        ResearchHomeViewModel model = Build(Funds, Returns, Facts);

        Assert.False(model.Funds.Unavailable);
        Assert.Equal(["Bonds", "US broad", null], model.FundGroups.Select(g => g.Role));
        Assert.Equal(["CCC", "BBB"], model.FundGroups[0].Rows.Select(r => r.Ticker));
        Assert.Equal(["AAA"], model.FundGroups[2].Rows.Select(r => r.Ticker));
        FundCompareRow ccc = model.FundGroups[0].Rows[0];
        // The three-year window, not the one-year one.
        Assert.Equal(4.5m, ccc.AnnualReturn);
        Assert.Equal(6m, ccc.Volatility);
        Assert.Null(ccc.ExpenseRatio);
        Assert.Equal(0.05m, model.FundGroups[0].Rows[1].ExpenseRatio);
        // AAA has no returns in the window: its figures are null, its name and size are still listed.
        FundCompareRow aaa = model.FundGroups[2].Rows[0];
        Assert.Null(aaa.AnnualReturn);
        Assert.Equal(5_000m, aaa.NetAssets);
    }

    [Fact]
    public void FundGroups_WithoutReturnsOrFacts_StillListEveryFund()
    {
        ResearchHomeViewModel model = Build(Funds);

        Assert.Null(model.FundReturns);
        Assert.Null(model.FundFacts);
        Assert.Equal(4, model.FundGroups.Sum(g => g.Rows.Count));
        Assert.All(model.FundGroups.SelectMany(g => g.Rows), r =>
        {
            Assert.Null(r.AnnualReturn);
            Assert.Null(r.ExpenseRatio);
        });
    }

    [Fact]
    public void Funds_WhenTheFundsDataIsMissing_IsUnavailable_WithRetryOnHome()
    {
        ResearchHomeViewModel model = Build(returns: Returns, facts: Facts);

        Assert.True(model.Funds.Unavailable);
        Assert.Empty(model.FundGroups);
        Assert.Equal("Index", model.Funds.UnavailableState.RetryAction);
        Assert.Equal("Research", model.Funds.UnavailableState.RetryController);
    }

    [Fact]
    public void Presets_WhenTheFundsLoaded_KeepOnlyTheMixesWhoseFundsAreAllTracked()
    {
        ResearchHomeViewModel model = Build(Funds);

        Assert.True(model.PresetsChecked);
        // Only SPY is tracked here, so only the S&P 500 mix stays.
        Assert.Equal(["Research.Home.Preset.SP500Only"], model.Presets.Select(p => p.LabelKey));
    }

    [Fact]
    public void Presets_WhenTheFundsDataIsMissing_AreAllOffered_ForTheScriptToCheck()
    {
        ResearchHomeViewModel model = Build();

        Assert.False(model.PresetsChecked);
        Assert.Equal(4, model.Presets.Count);
    }

    [Fact]
    public void Backdrop_TakesEachSeriesLatestMonth_AndTheTwelveMonthRiseInPrices()
    {
        var macro = new MacroSnapshotDto { Months = [] };
        var first = new DateOnly(2025, 1, 1);
        for (int i = 0; i < 18; i++)
        {
            macro.Months.Add(new MacroMonthDto
            {
                Month = first.AddMonths(i),
                ConsumerPrices = i <= 16 ? 300m + i : null,   // the last month has no CPI yet
                FedFunds = i <= 17 ? 4.33m : null,
                TreasuryBill = i <= 15 ? 4.2m : null,         // the bill rate stops two months earlier
            });
        }

        ResearchMarketBackdrop? backdrop = Build(macro: macro).Backdrop;

        Assert.NotNull(backdrop);
        Assert.False(backdrop.IsEmpty);
        Assert.Equal(new ResearchMarketFigure(4.33m, new DateOnly(2026, 6, 1)), backdrop.FedFunds);
        Assert.Equal(new ResearchMarketFigure(4.2m, new DateOnly(2026, 4, 1)), backdrop.TreasuryBill);
        // CPI 316 in May 2026 against 304 in May 2025.
        Assert.NotNull(backdrop.Inflation);
        Assert.Equal(new DateOnly(2026, 5, 1), backdrop.Inflation.Month);
        Assert.Equal(Math.Round((316m / 304m - 1m) * 100m, 6), Math.Round(backdrop.Inflation.Percent, 6));
    }

    [Fact]
    public void Backdrop_WithoutAYearOfPrices_HasNoInflation_AndIsEmptyWithNoSeries()
    {
        var macro = new MacroSnapshotDto
        {
            Months = [new MacroMonthDto { Month = new DateOnly(2026, 6, 1), ConsumerPrices = 320m }],
        };

        ResearchMarketBackdrop? backdrop = Build(macro: macro).Backdrop;

        Assert.NotNull(backdrop);
        Assert.Null(backdrop.Inflation);
        Assert.Null(backdrop.FedFunds);
        Assert.Null(backdrop.TreasuryBill);
        Assert.True(backdrop.IsEmpty);
        Assert.Null(Build().Backdrop);
    }

    [Fact]
    public void NotLoadedCount_CountsTheDatasetsWithoutAStatus()
    {
        var status = new ResearchStatusDto
        {
            Datasets =
            [
                new ResearchDatasetStatusDto { Dataset = "sectors", SourceAsOf = new DateOnly(2026, 10, 30) },
                new ResearchDatasetStatusDto { Dataset = "macro", SourceAsOf = new DateOnly(2026, 9, 30) },
            ],
        };

        Assert.Equal(6, Build(status: status).NotLoadedCount);
        Assert.Equal(8, Build().NotLoadedCount);
    }

    [Fact]
    public void TheView_ListsTheFundsWithBasketButtons_AndFoldsTheFreshnessIntoDetails()
    {
        string view = File.ReadAllText(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Views",
            "Research", "Index.cshtml"));

        // One basket toggle per fund, as the Fund page draws it, so research-basket.js wires it.
        Assert.Contains("data-research-basket-toggle=\"@row.Ticker\"", view, StringComparison.Ordinal);
        Assert.Contains("research-basket-toggle__add", view, StringComparison.Ordinal);
        Assert.Contains("research-basket-toggle__remove", view, StringComparison.Ordinal);
        // The role is a row-group heading and the fund links to its page with the basket kept.
        Assert.Contains("scope=\"rowgroup\"", view, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"research-home__fund-ticker"" href=""@\(fundHref \+ Uri\.EscapeDataString\(row\.Ticker\)\)"" data-research-link"), view);
        // The freshness list sits in a <details> whose summary carries the heading and the not-loaded count.
        Assert.Contains("<details class=\"research-card research-home__about\"", view, StringComparison.Ordinal);
        Assert.Contains("L.Plural(\"Research.Home.About.NotLoaded\", Model.NotLoadedCount)", view, StringComparison.Ordinal);
        // The mixes say whether they were checked here, so the script only checks them against the index otherwise.
        Assert.Contains("data-research-home-mixes-checked=\"@(Model.PresetsChecked ? \"true\" : \"false\")\"", view, StringComparison.Ordinal);
        // The search box keeps its id: the E2E walkthrough and the first-visit strip look for it.
        Assert.Contains("\"research-home-search\"", view, StringComparison.Ordinal);
    }
}
