using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
/// The Web Research pages read each dataset from ApiService through <see cref="IResearchApi"/>. A dataset that has
/// never loaded (503), or that cannot be read, shows that section's Unavailable State instead of an empty chart. The old
/// tabbed page's addresses redirect to the pages.
/// </summary>
public class ResearchWebControllerTests
{
    private static readonly SectorsSnapshotDto Snapshot = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Periods = ["2024", "2025"],
        Companies = [new SectorsCompanyDto { Cik = 320193, Name = "APPLE INC" }, new SectorsCompanyDto { Cik = 789019, Name = "MICROSOFT CORP" }],
        Values =
        [
            new SectorsPeriodDto { Period = "2024", Ranked = [new SectorsRankedValueDto { Company = 0, PublicFloat = 2m }] },
            new SectorsPeriodDto
            {
                Period = "2025",
                Ranked =
                [
                    new SectorsRankedValueDto { Company = 1, PublicFloat = 3_000m },
                    new SectorsRankedValueDto { Company = 0, PublicFloat = 2_600m },
                ],
                RestTotal = 40m,
                RestCount = 7,
            },
        ],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static async Task<ApiException> ApiFailure(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/sectors"),
            HttpMethod.Get, response, new RefitSettings());
    }

    [Fact]
    public async Task ReadAsync_LogsANeverLoadedDatasetAsOneLine_AndOtherFailuresAsWarningsWithTheError()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable,
            """{"reason":"The Sectors data has not loaded yet."}"""));
        proxy.Throws(nameof(IResearchApi.GetFundsAsync), await ApiFailure(HttpStatusCode.InternalServerError, "{}"));
        var logger = new RecordingLogger<ResearchController>();
        var controller = new ResearchController(api, TestResearchCache.Create(), logger, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.Sectors();

        (LogLevel Level, string Message, Exception? Exception) neverLoaded =
            Assert.Single(logger.Entries, e => e.Message.Contains("Sectors", StringComparison.Ordinal));
        Assert.Equal((LogLevel.Information, "The Research Sectors data has not loaded yet"), (neverLoaded.Level, neverLoaded.Message));
        Assert.Null(neverLoaded.Exception);
        (LogLevel Level, string Message, Exception? Exception) failed =
            Assert.Single(logger.Entries, e => e.Message.Contains("funds", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, failed.Level);
        Assert.NotNull(failed.Exception);
    }

    [Fact]
    public void Controller_RequiresSignIn()
    {
        Assert.NotNull(typeof(ResearchController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task ReadAsync_ReadsEachDatasetOncePerLifetime_AcrossTwoRequests()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Returns(nameof(IResearchApi.GetFundsAsync), new FundsSnapshotDto());
        ResearchController controller = CreateController(api);

        await controller.Sectors();
        await controller.Sectors();

        Assert.Equal(2, proxy.Calls.Count);
        Assert.Equal(2, proxy.Calls.Distinct().Count());
    }

    [Fact]
    public void Controller_HasOnlyThePagesAndTheDataTheirScriptsFetch()
    {
        // Besides the pages, the only actions are the search index and suggestions, the company lookup and press releases,
        // and the Sectors basket view's data, which it fetches when first shown so the page does not carry it.
        Assert.Equal(
            [
                nameof(ResearchController.Basket),
                nameof(ResearchController.Company), nameof(ResearchController.CompanyProfile), nameof(ResearchController.Fund),
                nameof(ResearchController.FundIndustries),
                nameof(ResearchController.Funds),
                nameof(ResearchController.Index),
                nameof(ResearchController.Releases), nameof(ResearchController.SearchIndex), nameof(ResearchController.Sectors),
                nameof(ResearchController.Suggest), nameof(ResearchController.World),
            ],
            typeof(ResearchController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Sectors_WhenApiServiceIsUnreachable_ShowsTheUnavailableState_WithoutExceptionDetails()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors());

        Assert.True(model.Sectors.Unavailable);
        Assert.DoesNotContain("unreachable", model.Sectors.UnavailableState.Heading, StringComparison.OrdinalIgnoreCase);
    }

    // Every dataset is read through one ReadAsync, so this proves its cancellation filter for all of them.
    [Fact]
    public async Task ReadAsync_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Sectors(cancellationToken: cts.Token));
    }

    // The old tabbed page's addresses keep working: each tab moves to its page, keeping a well-formed basket.
    [Theory]
    [InlineData("sectors", "Sectors", null, null)]
    [InlineData("SECTORS", "Sectors", null, null)]
    [InlineData("funds", "Basket", null, null)]
    [InlineData("etfs", "Basket", null, null)]
    [InlineData("performance", "Basket", null, "basket-growth")]
    [InlineData("risk", "Funds", "risk", null)]
    [InlineData("world", "World", null, null)]
    [InlineData("companies", "Company", null, null)]
    public async Task Index_RedirectsEachOldTab_KeepingTheBasket(string tab, string action, string? view, string? fragment)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(
            await CreateController(api).Index(tab, basket: " VTI:100 "));

        Assert.Equal((action, fragment), (redirect.ActionName, redirect.Fragment));
        Assert.Equal("VTI:100", redirect.RouteValues!["basket"]);
        Assert.Equal(view, redirect.RouteValues.TryGetValue("view", out object? v) ? v : null);
        Assert.Empty(proxy.Calls);
    }

    [Theory]
    [InlineData("brk.b", "BRK-B")] // As SEC lists it.
    [InlineData("<script>", null)]
    [InlineData("TOOLONGTICKER", null)]
    [InlineData(null, null)]
    public async Task Index_RedirectsTheOldCompaniesTab_WithAUsableTickerOnly(string? ticker, string? expected)
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(
            await CreateController(api).Index("companies", ticker, "SPY:60,<script>"));

        Assert.Equal(nameof(ResearchController.Company), redirect.ActionName);
        Assert.Equal(expected, redirect.RouteValues!["ticker"]);
        Assert.Null(redirect.RouteValues["basket"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public async Task Index_WithNoOrAnUnknownTab_ShowsHome(string? tab)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), new ResearchStatusDto());

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Index(tab));

        Assert.Null(view.ViewName);
        Assert.IsType<ResearchHomeViewModel>(view.Model);
    }

    private static readonly WorldSnapshotDto World = new()
    {
        AsOf = new DateOnly(2026, 10, 8),
        Periods = ["2024"],
        Countries = [new WorldCountryDto { Id = "840", Iso3 = "USA", Name = "United States" }],
        Values = [new WorldPeriodDto { Period = "2024", Countries = [new WorldCountryValueDto { Country = 0, MarketCap = 5e13m, PercentOfGdp = 172.4m }] }],
    };

    private static ResearchHomeViewModel HomeModelOf(IActionResult result) =>
        Assert.IsType<ResearchHomeViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static readonly ResearchStatusDto Status = new()
    {
        Datasets =
        [
            new ResearchDatasetStatusDto { Dataset = "etf-filings", SourceAsOf = new DateOnly(2026, 9, 30) },
            new ResearchDatasetStatusDto { Dataset = "etfs", SourceAsOf = new DateOnly(2026, 9, 30) },
            new ResearchDatasetStatusDto { Dataset = "fund-exposure", SourceAsOf = new DateOnly(2026, 9, 29) },
            new ResearchDatasetStatusDto { Dataset = "fund-facts", SourceAsOf = new DateOnly(2026, 8, 31) },
            new ResearchDatasetStatusDto { Dataset = "fund-overlap", SourceAsOf = new DateOnly(2026, 9, 28) },
            new ResearchDatasetStatusDto { Dataset = "fund-returns", SourceAsOf = new DateOnly(2026, 9, 27) },
            new ResearchDatasetStatusDto { Dataset = "fx-rates", SourceAsOf = new DateOnly(2026, 10, 30) },
            new ResearchDatasetStatusDto { Dataset = "sec-filers", SourceAsOf = new DateOnly(2026, 10, 30) },
            new ResearchDatasetStatusDto { Dataset = "sectors", SourceAsOf = new DateOnly(2026, 10, 30) },
            new ResearchDatasetStatusDto { Dataset = "world", SourceAsOf = new DateOnly(2026, 7, 1) },
        ],
    };

    [Fact]
    public async Task Home_ReadsOnlyTheDatasetStatus()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), Status);

        IActionResult result = await CreateController(api).Index();

        Assert.IsType<ViewResult>(result);
        Assert.Equal([nameof(IResearchApi.GetStatusAsync)], proxy.Calls);
    }

    [Fact]
    public async Task Home_ListsEachDatasetWithItsAsOf_AndNotLoadedForAMissingOne()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), Status);

        ResearchHomeViewModel model = HomeModelOf(await CreateController(api).Index());

        // The user-facing datasets in the page's order; the internal ones the status also lists are left out.
        Assert.Equal(
            [
                ("sectors", new DateOnly(2026, 10, 30)), ("etfs", new DateOnly(2026, 9, 30)),
                ("fund-returns", new DateOnly(2026, 9, 27)), ("fund-facts", new DateOnly(2026, 8, 31)),
                ("fund-overlap", new DateOnly(2026, 9, 28)), ("fund-exposure", new DateOnly(2026, 9, 29)),
                ("world", new DateOnly(2026, 7, 1)), ("macro", (DateOnly?)null),
            ],
            model.Datasets.Select(d => (d.Dataset, d.AsOf)));
        Assert.All(model.Datasets, d => Assert.StartsWith("Research.Home.Dataset.", d.LabelKey, StringComparison.Ordinal));
        Assert.Equal(model.Datasets.Count, model.Datasets.Select(d => d.LabelKey).Distinct().Count());
    }

    [Fact]
    public async Task Home_WhenTheStatusCannotBeRead_StillRendersWithEveryDatasetNotLoaded()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchHomeViewModel model = HomeModelOf(await CreateController(api).Index());

        Assert.Equal(8, model.Datasets.Count);
        Assert.All(model.Datasets, d => Assert.Null(d.AsOf));
        Assert.Equal(4, model.Presets.Count);
    }

    [Fact]
    public async Task Home_ReadsTheStatusThroughTheSnapshotCache_AndReadsAgainAfterAFailure()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        ResearchController controller = CreateController(api);

        ResearchHomeViewModel failed = HomeModelOf(await controller.Index());
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), Status);
        ResearchHomeViewModel first = HomeModelOf(await controller.Index());
        ResearchHomeViewModel second = HomeModelOf(await controller.Index());

        Assert.All(failed.Datasets, d => Assert.Null(d.AsOf));
        Assert.Equal(new DateOnly(2026, 10, 30), first.Datasets[0].AsOf);
        Assert.Equal(new DateOnly(2026, 10, 30), second.Datasets[0].AsOf);
        // The failure is not kept, and the status read after it is: two reads for three views.
        Assert.Equal(2, proxy.Calls.Count);
    }

    private static ResearchSectorsPageViewModel SectorsModelOf(IActionResult result) =>
        Assert.IsType<ResearchSectorsPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q2"],
        Funds = [new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" }],
        Values = [new FundsPeriodDto { Period = "2026 Q2", Funds = [new FundHoldingsDto { Ticker = "SPY", HoldingCount = 504 }] }],
    };

    [Fact]
    public async Task Sectors_ReadsSectorsAndFundsOnly()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors());

        Assert.Equal([nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetSectorsAsync)],
            proxy.Calls.Order(StringComparer.Ordinal));
        Assert.Same(Snapshot, model.Sectors.Snapshot);
        Assert.Same(Funds, model.Funds.Snapshot);
        Assert.Null(model.Focus);
        Assert.Equal(ResearchSectorsPageViewModel.MarketView, model.View);
    }

    [Fact]
    public async Task Sectors_WhenTheSectorsDataHasNeverLoaded_ShowsItsUnavailableState()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"))
            .Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors());

        Assert.True(model.Sectors.Unavailable);
        Assert.False(model.Funds.Unavailable);
        // Retry reloads this page, not the old tabbed one.
        Assert.Equal(("Research", "Sectors"),
            (model.Sectors.UnavailableState.RetryController, model.Sectors.UnavailableState.RetryAction));
        Assert.False(string.IsNullOrWhiteSpace(model.Sectors.UnavailableState.Heading));
    }

    [Theory]
    [InlineData("SPY:60,QQQ:40", true)]
    [InlineData("SPY,QQQ", true)]
    [InlineData("SPY:60,<script>", false)]
    [InlineData("SPY 60", false)]
    public async Task Sectors_WhenTheSectorsDataFails_KeepsAWellFormedBasketOnRetry(string basket, bool kept)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"))
            .Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchSectorsPageViewModel model = SectorsModelOf(
            await CreateController(api).Sectors(view: "basket", basket: basket));

        Assert.Equal(kept, model.Sectors.UnavailableState.RetryRouteValues.ContainsKey("basket"));
        if (kept)
        {
            Assert.Equal(basket, model.Sectors.UnavailableState.RetryRouteValues["basket"]);
        }
    }

    [Theory]
    [InlineData("SPY:60,QQQ:40", true)]
    [InlineData("SPY:60;DROP", false)]
    public async Task Sectors_WhenTheFundsDataFails_KeepsAWellFormedBasketOnRetry(string basket, bool kept)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Throws(nameof(IResearchApi.GetFundsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));

        ResearchSectorsPageViewModel model = SectorsModelOf(
            await CreateController(api).Sectors(view: "basket", basket: basket));

        Assert.Equal(kept, model.Funds.UnavailableState.RetryRouteValues.ContainsKey("basket"));
        if (kept)
        {
            Assert.Equal(basket, model.Funds.UnavailableState.RetryRouteValues["basket"]);
        }
    }

    [Fact]
    public async Task Sectors_WhenOnlyTheFundsDataFails_KeepsTheChart_AndOnlyTheBasketViewIsUnavailable()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Throws(nameof(IResearchApi.GetFundsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors(view: "basket"));

        Assert.False(model.Sectors.Unavailable);
        Assert.True(model.Funds.Unavailable);
        Assert.Equal(ResearchSectorsPageViewModel.BasketView, model.View);
        Assert.Equal(("Research", "Sectors"),
            (model.Funds.UnavailableState.RetryController, model.Funds.UnavailableState.RetryAction));
        Assert.Equal(ResearchSectorsPageViewModel.BasketView, model.Funds.UnavailableState.RetryRouteValues["view"]);
    }

    // On the market view the switch is not drawn without the funds data, so the panel says why instead of hiding it
    // silently; on the basket view the full Unavailable State says it.
    [Theory]
    [InlineData(null, true)]
    [InlineData("basket", false)]
    public async Task Sectors_WhenOnlyTheFundsDataFails_SaysTheBasketViewIsUnavailable(string? view, bool note)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Throws(nameof(IResearchApi.GetFundsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));

        ResearchSectorsPanelViewModel panel = SectorsModelOf(await CreateController(api).Sectors(view: view)).Panel;

        Assert.False(panel.BasketView);
        Assert.Equal(note, panel.BasketViewUnavailableNote);
        Assert.Equal(!note, panel.BasketUnavailableState is not null);
    }

    [Fact]
    public async Task Sectors_WhenEverythingLoads_ShowsTheSwitch_WithoutTheNote()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchSectorsPanelViewModel panel = SectorsModelOf(await CreateController(api).Sectors()).Panel;

        Assert.True(panel.BasketView);
        Assert.False(panel.BasketViewUnavailableNote);
    }

    [Theory]
    [InlineData("Technology", "Technology")]
    [InlineData(" Finance / National Commercial Banks ", "Finance/National Commercial Banks")]
    // Offices never hold a "/", but SIC industries can (SIC 2670), so only the first "/" separates the parts.
    [InlineData("Manufacturing/Converted Paper & Paperboard Prods (No Contaners/Boxes)",
        "Manufacturing/Converted Paper & Paperboard Prods (No Contaners/Boxes)")]
    [InlineData(" Finance / Banks/Thrifts ", "Finance/Banks/Thrifts")]
    [InlineData("Finance/", null)]
    [InlineData("/Banks", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task Sectors_KeepsAValidFocus_AndDropsAMalformedOne(string? focus, string? expected)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors(focus));

        Assert.Equal(expected, model.Focus);
    }

    [Fact]
    public async Task Sectors_DropsAFocusPartLongerThan80Characters()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();
        string eighty = new('a', 80);

        ResearchSectorsPageViewModel kept = SectorsModelOf(await CreateController(api).Sectors($"Finance/{eighty}"));
        ResearchSectorsPageViewModel dropped = SectorsModelOf(await CreateController(api).Sectors(eighty + "a"));

        Assert.Equal($"Finance/{eighty}", kept.Focus);
        Assert.Null(dropped.Focus);
    }

    [Theory]
    [InlineData("basket", "basket")]
    [InlineData("BASKET", "basket")]
    [InlineData("market", "market")]
    [InlineData("risk", "market")]
    [InlineData(null, "market")]
    public async Task Sectors_OpensOnTheMarketUnlessTheBasketViewIsAskedFor(string? view, string expected)
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchSectorsPageViewModel model = SectorsModelOf(await CreateController(api).Sectors(view: view));

        Assert.Equal(expected, model.View);
    }

    private static ResearchWorldPageViewModel WorldModelOf(IActionResult result) =>
        Assert.IsType<ResearchWorldPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static readonly FundExposureSnapshotDto Exposure = new()
    {
        AsOf = new DateOnly(2026, 9, 30),
        Funds =
        [
            new FundExposureDto
            {
                Ticker = "SPY",
                ReportDate = new DateOnly(2026, 9, 30),
                Countries = [new CountryShareDto { Code = "US", Percent = 99m }],
            },
        ],
    };

    /// <summary>Makes the World page's three datasets load, except <paramref name="failing"/>, which never has (503).</summary>
    private static async Task WithWorldData(ApiProxy<IResearchApi> proxy, string? failing = null)
    {
        (string Method, object Result)[] datasets =
        [
            (nameof(IResearchApi.GetWorldAsync), World),
            (nameof(IResearchApi.GetFundsAsync), Funds),
            (nameof(IResearchApi.GetFundExposureAsync), Exposure),
        ];
        foreach ((string method, object result) in datasets)
        {
            if (method == failing)
            {
                proxy.Throws(method, await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));
            }
            else
            {
                proxy.Returns(method, result);
            }
        }
    }

    [Fact]
    public async Task World_ReadsWorldFundsAndExposureOnly()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy);

        ResearchWorldPageViewModel model = WorldModelOf(await CreateController(api).World());

        Assert.Equal(
            [nameof(IResearchApi.GetFundExposureAsync), nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetWorldAsync)],
            proxy.Calls.Order(StringComparer.Ordinal));
        Assert.Same(World, model.World.Snapshot);
        Assert.Same(Funds, model.Funds.Snapshot);
        Assert.Same(Exposure, model.Exposure);
        Assert.Null(model.Country);
        Assert.Equal(ResearchWorldPageViewModel.MarketView, model.View);
        Assert.True(model.Panel.BasketView);
        Assert.Null(model.Panel.BasketUnavailableState);
    }

    [Fact]
    public async Task World_WhenTheWorldDataHasNeverLoaded_StillShowsTheBasketView()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy, nameof(IResearchApi.GetWorldAsync));

        ResearchWorldPageViewModel model = WorldModelOf(
            await CreateController(api).World(view: "basket", basket: "SPY:60,QQQ:40"));

        Assert.True(model.World.Unavailable);
        Assert.True(model.Panel.BasketView);
        Assert.Null(model.Panel.BasketUnavailableState);
        // Only the map is unavailable: its Retry reloads this page, in the basket view, with the basket kept.
        UnavailableStateViewModelAssert(model.World.UnavailableState, "basket", "SPY:60,QQQ:40");
    }

    [Theory]
    [InlineData(nameof(IResearchApi.GetFundExposureAsync))]
    [InlineData(nameof(IResearchApi.GetFundsAsync))]
    public async Task World_WhenTheBasketDataFails_KeepsTheMap_AndOnlyTheBasketViewIsUnavailable(string failing)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy, failing);

        ResearchWorldPageViewModel model = WorldModelOf(
            await CreateController(api).World(view: "basket", basket: "SPY:60,QQQ:40"));

        Assert.False(model.World.Unavailable);
        Assert.False(model.Panel.BasketView);
        Assert.NotNull(model.Panel.BasketUnavailableState);
        UnavailableStateViewModelAssert(model.Panel.BasketUnavailableState, "basket", "SPY:60,QQQ:40");
    }

    [Fact]
    public async Task World_WhenTheBasketDataFails_OnTheMarketView_ShowsNoBasketUnavailableState()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy, nameof(IResearchApi.GetFundExposureAsync));

        ResearchWorldPageViewModel model = WorldModelOf(await CreateController(api).World());

        Assert.False(model.Panel.BasketView);
        Assert.Null(model.Panel.BasketUnavailableState);
    }

    [Theory]
    [InlineData("SPY:60,QQQ:40", true)]
    [InlineData("SPY:60,<script>", false)]
    public async Task World_WhenTheWorldDataFails_KeepsAWellFormedBasketOnRetry(string basket, bool kept)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy, nameof(IResearchApi.GetWorldAsync));

        ResearchWorldPageViewModel model = WorldModelOf(await CreateController(api).World(basket: basket));

        Assert.Equal(kept, model.World.UnavailableState.RetryRouteValues.ContainsKey("basket"));
    }

    [Theory]
    [InlineData("Japan", "Japan")]
    [InlineData("  japan ", "japan")]
    [InlineData("Korea, Rep.", "Korea, Rep.")]
    [InlineData("Côte d'Ivoire", "Côte d'Ivoire")]
    [InlineData("Hong Kong SAR (China)", "Hong Kong SAR (China)")]
    [InlineData("<script>alert(1)</script>", null)]
    [InlineData("Japan\"onload=\"x", null)]
    [InlineData("Japan\nChina", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task World_KeepsAWellFormedCountry_AndDropsAMalformedOne(string? country, string? expected)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        await WithWorldData(proxy);

        ResearchWorldPageViewModel model = WorldModelOf(await CreateController(api).World(country));

        Assert.Equal(expected, model.Country);
        Assert.Equal(expected, model.Panel.Country);
    }

    [Fact]
    public async Task World_DropsACountryLongerThan80Characters_AndKeepsAWellFormedOneOnRetry()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetWorldAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));
        string eighty = new('a', 80);

        ResearchWorldPageViewModel kept = WorldModelOf(await CreateController(api).World(eighty));
        ResearchWorldPageViewModel dropped = WorldModelOf(await CreateController(api).World(eighty + "a"));

        Assert.Equal(eighty, kept.Country);
        Assert.Equal(eighty, kept.World.UnavailableState.RetryRouteValues["country"]);
        Assert.Null(dropped.Country);
        Assert.False(dropped.World.UnavailableState.RetryRouteValues.ContainsKey("country"));
    }

    [Theory]
    [InlineData("basket", "basket")]
    [InlineData("BASKET", "basket")]
    [InlineData("market", "market")]
    [InlineData("risk", "market")]
    [InlineData(null, "market")]
    public async Task World_OpensOnTheMarketUnlessTheBasketViewIsAskedFor(string? view, string expected)
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchWorldPageViewModel model = WorldModelOf(await CreateController(api).World(view: view));

        Assert.Equal(expected, model.View);
    }

    /// <summary>The Unavailable State's Retry reloads the World page with <paramref name="view"/> and <paramref name="basket"/>.</summary>
    private static void UnavailableStateViewModelAssert(PersonalFinance.Web.Infrastructure.UnavailableStateViewModel state,
        string view, string basket)
    {
        Assert.Equal(("Research", "World"), (state.RetryController, state.RetryAction));
        Assert.False(string.IsNullOrWhiteSpace(state.Heading));
        Assert.Equal(view, state.RetryRouteValues["view"]);
        Assert.Equal(basket, state.RetryRouteValues["basket"]);
    }
}
