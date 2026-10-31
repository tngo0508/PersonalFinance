using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Research;
using ApiException = Refit.ApiException;
using RefitSettings = Refit.RefitSettings;
using ResearchController = PersonalFinance.Web.Controllers.ResearchController;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Funds page: the comparison table's sort keys, window and quarter from the address with their
/// fallbacks, each dataset's own Unavailable State, and the holdings list that links holdings by name only (ADR 0010).
/// </summary>
public class ResearchFundsPageTests
{
    // Three funds: BBB and CCC tie on return and volatility so the tie-break by ticker shows; AAA has no role, no
    // returns for 5 years and no filed facts.
    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q1", "2026 Q2"],
        Funds =
        [
            new TrackedFundDto { Ticker = "CCC", Name = "Charlie Fund", Role = "Bonds" },
            new TrackedFundDto { Ticker = "AAA", Name = "Alpha Fund" },
            new TrackedFundDto { Ticker = "BBB", Name = "Bravo Fund", Role = "Bonds" },
        ],
        Values =
        [
            new FundsPeriodDto
            {
                Period = "2026 Q1",
                Funds = [new FundHoldingsDto { Ticker = "AAA", NetAssets = 1_000m, HoldingCount = 1 }],
            },
            new FundsPeriodDto
            {
                Period = "2026 Q2",
                Funds =
                [
                    new FundHoldingsDto
                    {
                        Ticker = "AAA", NetAssets = 5_000m, HoldingCount = 30,
                        Categories =
                        [
                            new FundCategoryDto
                            {
                                Code = "EC", Name = "Equity, common", Percent = 99m, HoldingCount = 30,
                                Top = [new FundHoldingDto { Name = "NVIDIA CORP", Percent = 7m }, new FundHoldingDto { Name = "AT&T Inc.", Percent = 1m }],
                                OtherCount = 28, OtherPercent = 91m,
                            },
                        ],
                    },
                    new FundHoldingsDto { Ticker = "BBB", NetAssets = 2_000m, HoldingCount = 10 },
                    new FundHoldingsDto { Ticker = "CCC", NetAssets = 2_000m, HoldingCount = 10 },
                ],
            },
        ],
    };

    private static readonly FundReturnsSnapshotDto Returns = new()
    {
        AsOf = new DateOnly(2026, 6, 1),
        Windows =
        [
            new FundReturnsWindowDto
            {
                Years = 3,
                Funds =
                [
                    Metrics("AAA", annualized: 9m, volatility: 20m, sharpe: 0.3m),
                    Metrics("BBB", annualized: 4m, volatility: 6m, sharpe: 0.1m),
                    Metrics("CCC", annualized: 4m, volatility: 6m, sharpe: 0.1m),
                ],
            },
            new FundReturnsWindowDto
            {
                Years = 5,
                Funds = [Metrics("BBB", annualized: 3m, volatility: 5m, sharpe: null), Metrics("CCC", annualized: 2m, volatility: 5m, sharpe: 0.2m)],
            },
        ],
    };

    private static readonly FundFactsSnapshotDto Facts = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds =
        [
            new FundFactsDto { Ticker = "BBB", ExpenseRatio = 0.03m },
            new FundFactsDto { Ticker = "CCC", ExpenseRatio = 0.03m },
            // AAA (like SPY) files no facts: its fee shows "not filed".
        ],
    };

    private static FundWindowMetricsDto Metrics(string ticker, decimal annualized, decimal volatility, decimal? sharpe) =>
        new() { Ticker = ticker, AnnualizedReturn = annualized, Volatility = volatility, Sharpe = sharpe };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchFundsPageViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ResearchFundsPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<ApiException> NeverLoaded() =>
        await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/funds"),
            HttpMethod.Get, new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }, new RefitSettings());

    private static (IResearchApi Api, ApiProxy<IResearchApi> Proxy) AllLoaded()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds)
            .Returns(nameof(IResearchApi.GetFundReturnsAsync), Returns)
            .Returns(nameof(IResearchApi.GetFundFactsAsync), Facts);
        return (api, proxy);
    }

    [Fact]
    public async Task Funds_ReadsFundsReturnsAndFactsOnly()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds());

        Assert.Equal([nameof(IResearchApi.GetFundFactsAsync), nameof(IResearchApi.GetFundReturnsAsync), nameof(IResearchApi.GetFundsAsync)],
            proxy.Calls.Order(StringComparer.Ordinal));
        Assert.Same(Funds, model.Funds.Snapshot);
        Assert.Same(Returns, model.FundReturns);
        Assert.Same(Facts, model.FundFacts);
    }

    [Theory]
    [InlineData("ticker", new[] { "AAA", "BBB", "CCC" })]
    [InlineData("role", new[] { "BBB", "CCC", "AAA" })]
    [InlineData("return", new[] { "AAA", "BBB", "CCC" })]
    [InlineData("volatility", new[] { "AAA", "BBB", "CCC" })]
    [InlineData("sharpe", new[] { "AAA", "BBB", "CCC" })]
    [InlineData("fee", new[] { "BBB", "CCC", "AAA" })]
    [InlineData("assets", new[] { "AAA", "BBB", "CCC" })]
    [InlineData("RETURN", new[] { "AAA", "BBB", "CCC" })]
    public async Task Funds_SortsByTheChosenColumn_TiesByTicker(string sort, string[] expected)
    {
        (IResearchApi api, _) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(sort: sort));

        Assert.Equal(expected, model.Rows.Select(r => r.Ticker));
        Assert.Equal(sort.ToLowerInvariant(), model.Sort);
    }

    [Fact]
    public async Task Funds_SortsAMissingFigureLast_AndUsesTheChosenWindowsFigures()
    {
        (IResearchApi api, _) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(window: 5, sort: "sharpe"));

        // Over 5 years AAA has no figures and BBB no Sharpe ratio: both sort after CCC, the missing ones by ticker.
        Assert.Equal(["CCC", "AAA", "BBB"], model.Rows.Select(r => r.Ticker));
        Assert.Equal(5, model.Window);
        Assert.Equal(3m, model.Rows.Single(r => r.Ticker == "BBB").AnnualReturn);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("pie", 7, "name", "1999 Q4")]
    [InlineData("<script>", 0, "", "")]
    [InlineData("  ", -3, "drop table", "2026 Q3")]
    public async Task Funds_IgnoresAnUnknownViewWindowSortOrQuarter(string? view, int? window, string? sort, string? quarter)
    {
        (IResearchApi api, _) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(view, window, sort, quarter));

        Assert.Equal(ResearchFundsPageViewModel.TableView, model.View);
        Assert.Equal(3, model.Window);
        Assert.Equal(ResearchFundsPageViewModel.SortReturn, model.Sort);
        Assert.Equal("2026 Q2", model.HoldingsPeriod?.Period);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("risk")]
    [InlineData("Correlation")]
    [InlineData("holdings")]
    public async Task Funds_OpensOnTheViewAskedFor(string view)
    {
        (IResearchApi api, _) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(view: view, window: 1, quarter: "2026 q1"));

        Assert.Equal(view.ToLowerInvariant(), model.View);
        Assert.Equal(1, model.Window);
        Assert.Equal("2026 Q1", model.HoldingsPeriod?.Period);
        Assert.Equal("2026 Q1", model.Holdings?.Period.Period);
        Assert.Equal(Funds.Periods, model.Holdings?.Quarters);
    }

    [Fact]
    public async Task Funds_AFundWithoutFiledFacts_HasNoFee_AndEachFundItsLatestNetAssets()
    {
        (IResearchApi api, _) = AllLoaded();

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds());

        FundCompareRow alpha = model.Rows.Single(r => r.Ticker == "AAA");
        Assert.Null(alpha.ExpenseRatio);
        Assert.Equal(5_000m, alpha.NetAssets);
        Assert.Equal(0.03m, model.Rows.Single(r => r.Ticker == "BBB").ExpenseRatio);
    }

    [Fact]
    public async Task Funds_WhenTheReturnsHaveNeverLoaded_ShowsTheTableWithoutReturns_AndTheRiskViewUnavailable()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds)
            .Throws(nameof(IResearchApi.GetFundReturnsAsync), await NeverLoaded())
            .Returns(nameof(IResearchApi.GetFundFactsAsync), Facts);

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(view: "risk", basket: "BBB:60,CCC:40"));

        Assert.False(model.Funds.Unavailable);
        Assert.Equal(3, model.Rows.Count);
        Assert.All(model.Rows, r => Assert.Null(r.AnnualReturn));
        Assert.Equal(0.03m, model.Rows.Single(r => r.Ticker == "BBB").ExpenseRatio);
        Assert.True(model.RiskUnavailable);
        Assert.Equal(("Research", "Funds"), (model.ReturnsUnavailableState.RetryController, model.ReturnsUnavailableState.RetryAction));
        Assert.Equal("risk", model.ReturnsUnavailableState.RetryRouteValues["view"]);
        Assert.Equal("BBB:60,CCC:40", model.ReturnsUnavailableState.RetryRouteValues["basket"]);
    }

    [Fact]
    public async Task Funds_WhenTheFundsDataHasNeverLoaded_ShowsItsUnavailableState()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetFundsAsync), await NeverLoaded())
            .Returns(nameof(IResearchApi.GetFundReturnsAsync), Returns)
            .Returns(nameof(IResearchApi.GetFundFactsAsync), Facts);

        ResearchFundsPageViewModel model = ModelOf(await CreateController(api).Funds(view: "holdings", basket: "SPY:60,<script>"));

        Assert.True(model.Funds.Unavailable);
        Assert.Empty(model.Rows);
        Assert.Null(model.Holdings);
        Assert.True(model.RiskUnavailable);
        Assert.Equal("Funds", model.Funds.UnavailableState.RetryAction);
        Assert.Equal("holdings", model.Funds.UnavailableState.RetryRouteValues["view"]);
        Assert.False(model.Funds.UnavailableState.RetryRouteValues.ContainsKey("basket"));
    }

    [Theory]
    [InlineData("NVIDIA CORP", "?q=NVIDIA%20CORP")]
    [InlineData("AT&T Inc.", "?q=AT%26T%20Inc.")]
    [InlineData("  Apple Inc  ", "?q=Apple%20Inc")]
    public void FundHoldingsList_LinksAHoldingToTheCompanySearchByItsName(string name, string expected) =>
        Assert.Equal(expected, FundHoldingsListViewModel.CompanySearchQuery(name));

    [Fact]
    public void FundHoldingsList_LinksHoldingsToTheCompanySearchByName_NeverByIdentifier()
    {
        // The same ADR 0010 shapes the funds refresh is held to (ResearchFundsServiceTests).
        var cusipShaped = new Regex(@"\b[0-9A-Z]{8}[0-9]\b");
        var isinShaped = new Regex(@"\b[A-Z]{2}[0-9A-Z]{9}[0-9]\b");
        IEnumerable<string> links = Funds.Values.SelectMany(p => p.Funds).SelectMany(f => f.Categories)
            .SelectMany(c => c.Top).Select(h => "/Research/Company" + FundHoldingsListViewModel.CompanySearchQuery(h.Name));
        Assert.All(links, link =>
        {
            Assert.False(cusipShaped.IsMatch(link), $"A CUSIP-shaped token is in {link}.");
            Assert.False(isinShaped.IsMatch(link), $"An ISIN-shaped token is in {link}.");
        });

        // The partial links by name through the same helper, keeps the basket, and names no identifier field.
        string partial = File.ReadAllText(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), "Views",
            "Research", "_FundHoldingsList.cshtml"));
        Assert.Contains("FundHoldingsListViewModel.CompanySearchQuery(holding.Name)", partial, StringComparison.Ordinal);
        Assert.Contains("data-research-link", partial, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\.(Cusip|Isin|Lei)\b"), partial);
    }

    [Fact]
    public void TheHoldingsTree_IsGone_AndNothingReferencesIt()
    {
        string web = RepositoryRoot.SourceProject("PersonalFinance.Web");
        Assert.False(File.Exists(Path.Combine(web, "wwwroot", "js", "research", "research-funds.js")));
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "wwwroot", "js", "research"), "*.js"))
            .Concat(Directory.EnumerateFiles(Path.Combine(web, "wwwroot", "css"), "research*.css"));
        Assert.All(files, file =>
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("research-funds.js", text, StringComparison.Ordinal);
            Assert.DoesNotContain("research-funds-tree", text, StringComparison.Ordinal);
            Assert.DoesNotContain("research-funds__", text, StringComparison.Ordinal);
        });
    }
}
