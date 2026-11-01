using System.Net;
using System.Text;
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
/// The Research Fund page: it reads its six datasets only, finds the fund whatever the ticker's case, answers 404 with
/// the "not found" page for a rejected or untracked ticker, and keeps the holdings sections when the returns have never
/// loaded.
/// </summary>
public class ResearchFundPageTests
{
    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q1", "2026 Q2"],
        Funds =
        [
            new TrackedFundDto { Ticker = "VTI", Name = "Vanguard Total Stock Market ETF", Role = "US broad" },
            new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" },
        ],
        Values =
        [
            new FundsPeriodDto
            {
                Period = "2026 Q1",
                Funds = [new FundHoldingsDto { Ticker = "VTI", HoldingCount = 3500, NetAssets = 1m }],
            },
            new FundsPeriodDto
            {
                Period = "2026 Q2",
                Funds =
                [
                    new FundHoldingsDto
                    {
                        Ticker = "VTI",
                        HoldingCount = 3600,
                        NetAssets = 1_900_000_000_000m,
                        Categories =
                        [
                            new FundCategoryDto
                            {
                                Name = "Equity, common",
                                Top =
                                [
                                    new FundHoldingDto { Name = "Apple Inc", Percent = 6.1m },
                                    new FundHoldingDto { Name = "NVIDIA Corp", Percent = 6.4m },
                                ],
                            },
                            new FundCategoryDto { Name = "Cash", Top = [new FundHoldingDto { Name = "Money market", Percent = 0.3m }] },
                        ],
                    },
                ],
            },
        ],
    };

    private static readonly FundOverlapSnapshotDto Overlap = new() { AsOf = new DateOnly(2026, 6, 30) };
    private static readonly MacroSnapshotDto Macro = new();
    private static readonly FundFactsSnapshotDto Facts = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds = [new FundFactsDto { Ticker = "VTI", ExpenseRatio = 0.03m }],
    };

    private static readonly FundReturnsSnapshotDto Returns = new()
    {
        AsOf = new DateOnly(2026, 6, 1),
        Windows = [new FundReturnsWindowDto { Years = 3, Funds = [new FundWindowMetricsDto { Ticker = "VTI", AnnualizedReturn = 11.2m }] }],
    };

    private static readonly FundExposureSnapshotDto Exposure = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds = [new FundExposureDto { Ticker = "VTI", Countries = [new CountryShareDto { Code = "US", Percent = 99m }] }],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchFundPageViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ResearchFundPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<ApiException> NeverLoaded() =>
        await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/fund-returns"),
            HttpMethod.Get, new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }, new RefitSettings());

    /// <summary>Every dataset loaded, except <paramref name="neverLoaded"/>, which answers 503.</summary>
    private static async Task<(IResearchApi Api, ApiProxy<IResearchApi> Proxy)> AllLoadedExcept(string? neverLoaded = null)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        Dictionary<string, object> datasets = new()
        {
            [nameof(IResearchApi.GetFundsAsync)] = Funds,
            [nameof(IResearchApi.GetFundOverlapAsync)] = Overlap,
            [nameof(IResearchApi.GetFundReturnsAsync)] = Returns,
            [nameof(IResearchApi.GetMacroAsync)] = Macro,
            [nameof(IResearchApi.GetFundFactsAsync)] = Facts,
            [nameof(IResearchApi.GetFundExposureAsync)] = Exposure,
        };
        foreach ((string method, object snapshot) in datasets)
        {
            if (method == neverLoaded)
            {
                proxy.Throws(method, await NeverLoaded());
            }
            else
            {
                proxy.Returns(method, snapshot);
            }
        }
        return (api, proxy);
    }

    [Fact]
    public async Task Fund_ReadsItsSixDatasets()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = await AllLoadedExcept();

        ResearchFundPageViewModel model = ModelOf(await CreateController(api).Fund("VTI", "VTI:60,SPY:40"));

        Assert.Equal(
            [
                nameof(IResearchApi.GetFundExposureAsync), nameof(IResearchApi.GetFundFactsAsync),
                nameof(IResearchApi.GetFundOverlapAsync), nameof(IResearchApi.GetFundReturnsAsync),
                nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetMacroAsync),
            ],
            proxy.Calls.Order(StringComparer.Ordinal));
        Assert.Same(Overlap, model.FundOverlap);
        Assert.Same(Returns, model.FundReturns);
        Assert.Same(Macro, model.Macro);
        Assert.Same(Facts, model.FundFacts);
        Assert.Same(Exposure, model.FundExposure);
    }

    [Fact]
    public async Task Fund_IsCaseInsensitive_AndSummarisesTheFundsLatestReport()
    {
        (IResearchApi api, _) = await AllLoadedExcept();

        ResearchFundPageViewModel model = ModelOf(await CreateController(api).Fund("vti"));

        Assert.Equal("VTI", model.Fund.Ticker);
        Assert.Equal("2026 Q2", model.HoldingsPeriod);
        Assert.Equal(1_900_000_000_000m, model.NetAssets);
        Assert.Equal(0.03m, model.ExpenseRatio);
        Assert.Equal(11.2m, model.AnnualizedReturns[3]);
        Assert.Null(model.AnnualizedReturns[1]);
        Assert.Equal(["NVIDIA Corp", "Apple Inc", "Money market"], model.TopHoldings.Select(h => h.Name));
        Assert.Equal("Equity, common", model.TopHoldings[0].Category);
        Assert.Equal("US", Assert.Single(model.TopCountries).Code);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("<script>")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Fund_ForAnUnknownOrRejectedTicker_Returns404NotFoundView(string? ticker)
    {
        (IResearchApi api, _) = await AllLoadedExcept();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Fund(ticker));

        Assert.Equal("NotFound", view.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
        ResearchNotFoundViewModel model = Assert.IsType<ResearchNotFoundViewModel>(view.Model);
        Assert.Null(model.FundsUnavailableState);
        // A rejected ticker is never repeated; an untracked one is, as checked.
        Assert.Equal(ticker == "xyz" ? "XYZ" : null, model.Ticker);
    }

    [Fact]
    public async Task Fund_WhenTheFundsDataHasNeverLoaded_Returns404WithItsUnavailableState()
    {
        (IResearchApi api, _) = await AllLoadedExcept(nameof(IResearchApi.GetFundsAsync));

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Fund("VTI"));

        Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
        ResearchNotFoundViewModel model = Assert.IsType<ResearchNotFoundViewModel>(view.Model);
        Assert.NotNull(model.FundsUnavailableState);
        Assert.Equal(("Fund", "VTI"), (model.FundsUnavailableState.RetryAction, model.FundsUnavailableState.RetryRouteValues["ticker"]));
    }

    [Fact]
    public async Task Fund_WhenTheReturnsHaveNeverLoaded_KeepsTheHoldingsSections()
    {
        (IResearchApi api, _) = await AllLoadedExcept(nameof(IResearchApi.GetFundReturnsAsync));

        ResearchFundPageViewModel model = ModelOf(await CreateController(api).Fund("VTI", "VTI:100"));

        Assert.Null(model.FundReturns);
        Assert.Equal(("Fund", "VTI", "VTI:100"), (model.ReturnsUnavailableState.RetryAction,
            model.ReturnsUnavailableState.RetryRouteValues["ticker"], model.ReturnsUnavailableState.RetryRouteValues["basket"]));
        Assert.Equal(3, model.TopHoldings.Count);
        Assert.NotNull(model.Exposure);
        Assert.Same(Overlap, model.FundOverlap);
    }
}
