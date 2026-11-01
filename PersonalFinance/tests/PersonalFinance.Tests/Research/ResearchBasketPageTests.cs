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
/// The Research Basket page: it reads its seven datasets only, each missing one leaves only the sections that need it
/// unavailable (keeping the basket on Retry), and a basket without a Tracked Fund opens on the prompt with the starting
/// mixes whose funds are all tracked.
/// </summary>
public class ResearchBasketPageTests
{
    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q2"],
        Funds =
        [
            new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" },
            new TrackedFundDto { Ticker = "QQQ", Name = "Invesco QQQ Trust" },
        ],
        Values = [new FundsPeriodDto { Period = "2026 Q2", Funds = [new FundHoldingsDto { Ticker = "SPY", HoldingCount = 503 }] }],
    };

    private static readonly SectorsSnapshotDto Sectors = new()
    {
        AsOf = new DateOnly(2025, 6, 30),
        Periods = ["2025"],
        Values = [new SectorsPeriodDto { Period = "2025" }],
    };

    private static readonly FundOverlapSnapshotDto Overlap = new() { AsOf = new DateOnly(2026, 6, 30) };
    private static readonly FundReturnsSnapshotDto Returns = new() { AsOf = new DateOnly(2026, 6, 1) };
    private static readonly MacroSnapshotDto Macro = new();
    private static readonly FundFactsSnapshotDto Facts = new() { AsOf = new DateOnly(2026, 6, 30) };

    private static readonly FundExposureSnapshotDto Exposure = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds = [new FundExposureDto { Ticker = "SPY", Countries = [new CountryShareDto { Code = "US", Percent = 100m }] }],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchBasketPageViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ResearchBasketPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<ApiException> NeverLoaded() =>
        await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/fund-overlap"),
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
            [nameof(IResearchApi.GetSectorsAsync)] = Sectors,
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
    public async Task Basket_ReadsItsSevenDatasets()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = await AllLoadedExcept();

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket("SPY:60,QQQ:40"));

        Assert.Equal(
            [
                nameof(IResearchApi.GetFundExposureAsync), nameof(IResearchApi.GetFundFactsAsync),
                nameof(IResearchApi.GetFundOverlapAsync), nameof(IResearchApi.GetFundReturnsAsync),
                nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetMacroAsync), nameof(IResearchApi.GetSectorsAsync),
            ],
            proxy.Calls.Order(StringComparer.Ordinal));
        Assert.Same(Funds, model.Funds.Snapshot);
        Assert.Same(Overlap, model.FundOverlap);
        Assert.Same(Returns, model.FundReturns);
        Assert.Same(Macro, model.Macro);
        Assert.Same(Facts, model.FundFacts);
        Assert.Same(Exposure, model.FundExposure);
        Assert.Same(Sectors, model.Sectors.Snapshot);
        Assert.False(model.IsEmpty);
        Assert.True(model.WorldPanel.BasketView);
        Assert.True(model.WorldPanel.BasketOnly);
        Assert.Equal(("basket", true), (model.SectorsPanel.View, model.SectorsPanel.FixedAddress));
    }

    [Fact]
    public async Task Basket_WhenTheOverlapHasNeverLoaded_ShowsOnlyThatSectionUnavailable()
    {
        (IResearchApi api, _) = await AllLoadedExcept(nameof(IResearchApi.GetFundOverlapAsync));

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket("SPY:60,QQQ:40"));

        Assert.Null(model.FundOverlap);
        Assert.Equal(("Research", "Basket"), (model.OverlapUnavailableState.RetryController, model.OverlapUnavailableState.RetryAction));
        Assert.Equal("SPY:60,QQQ:40", model.OverlapUnavailableState.RetryRouteValues["basket"]);
        Assert.False(model.Funds.Unavailable);
        Assert.Same(Returns, model.FundReturns);
        Assert.Same(Exposure, model.FundExposure);
        Assert.False(model.Sectors.Unavailable);
    }

    [Fact]
    public async Task Basket_WhenTheExposureHasNeverLoaded_LeavesOnlyTheCountriesUnavailable()
    {
        (IResearchApi api, _) = await AllLoadedExcept(nameof(IResearchApi.GetFundExposureAsync));

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket("SPY"));

        Assert.Null(model.FundExposure);
        Assert.False(model.WorldPanel.BasketView);
        Assert.Equal("SPY", model.ExposureUnavailableState.RetryRouteValues["basket"]);
        Assert.Same(Overlap, model.FundOverlap);
    }

    [Fact]
    public async Task Basket_WhenTheFundsDataHasNeverLoaded_ShowsItsUnavailableState_WithoutAMalformedBasket()
    {
        (IResearchApi api, _) = await AllLoadedExcept(nameof(IResearchApi.GetFundsAsync));

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket("SPY:60,<script>"));

        Assert.True(model.Funds.Unavailable);
        Assert.Equal("Basket", model.Funds.UnavailableState.RetryAction);
        Assert.False(model.Funds.UnavailableState.RetryRouteValues.ContainsKey("basket"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XYZ:100")]
    [InlineData("<script>")]
    public async Task Basket_WithoutATrackedFund_OpensOnThePrompt(string? basket)
    {
        (IResearchApi api, _) = await AllLoadedExcept();

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket(basket));

        Assert.True(model.IsEmpty);
    }

    [Theory]
    [InlineData("spy")]
    [InlineData("XYZ:50,QQQ:50")]
    public async Task Basket_WithATrackedFund_WhateverItsCase_OpensOnTheSections(string basket)
    {
        (IResearchApi api, _) = await AllLoadedExcept();

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket(basket));

        Assert.False(model.IsEmpty);
    }

    [Fact]
    public async Task Basket_OffersOnlyTheStartingMixesWhoseFundsAreAllTracked()
    {
        (IResearchApi api, _) = await AllLoadedExcept();

        ResearchBasketPageViewModel model = ModelOf(await CreateController(api).Basket());

        // Of the four mixes only "S&P 500 only" (SPY) is made of SPY and QQQ alone.
        ResearchBasketPreset preset = Assert.Single(model.Presets);
        Assert.Equal("SPY:100", preset.Basket);
    }
}
