using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
/// The Web Research page reads each dataset from ApiService through <see cref="IResearchApi"/>. A dataset that has
/// never loaded (503), or that cannot be read, shows that tab's Unavailable State instead of an empty chart.
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

    private static ResearchViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<ResearchViewModel>(Assert.IsType<ViewResult>(result).Model);

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
    public async Task Index_LogsANeverLoadedDatasetAsOneLine_AndOtherFailuresAsWarningsWithTheError()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);
        proxy.Throws(nameof(IResearchApi.GetFundOverlapAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable,
            """{"reason":"The fund overlap data has not loaded yet."}"""));
        proxy.Throws(nameof(IResearchApi.GetWorldAsync), await ApiFailure(HttpStatusCode.InternalServerError, "{}"));
        var logger = new RecordingLogger<ResearchController>();
        var controller = new ResearchController(api, TestResearchCache.Create(), logger, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.Index();

        (LogLevel Level, string Message, Exception? Exception) neverLoaded =
            Assert.Single(logger.Entries, e => e.Message.Contains("fund overlap", StringComparison.Ordinal));
        Assert.Equal((LogLevel.Information, "The Research fund overlap data has not loaded yet"), (neverLoaded.Level, neverLoaded.Message));
        Assert.Null(neverLoaded.Exception);
        (LogLevel Level, string Message, Exception? Exception) failed =
            Assert.Single(logger.Entries, e => e.Message.Contains("World", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, failed.Level);
        Assert.NotNull(failed.Exception);
    }

    [Fact]
    public void Controller_RequiresSignIn()
    {
        Assert.NotNull(typeof(ResearchController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Index_ReadsEachDatasetOncePerLifetime_AcrossTwoRequests()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot)
            .Returns(nameof(IResearchApi.GetFundsAsync), new FundsSnapshotDto())
            .Returns(nameof(IResearchApi.GetWorldAsync), new WorldSnapshotDto())
            .Returns(nameof(IResearchApi.GetFundOverlapAsync), new FundOverlapSnapshotDto())
            .Returns(nameof(IResearchApi.GetFundReturnsAsync), new FundReturnsSnapshotDto())
            .Returns(nameof(IResearchApi.GetMacroAsync), new MacroSnapshotDto())
            .Returns(nameof(IResearchApi.GetFundFactsAsync), new FundFactsSnapshotDto())
            .Returns(nameof(IResearchApi.GetFundExposureAsync), new FundExposureSnapshotDto());
        ResearchController controller = CreateController(api);

        await controller.Index();
        await controller.Index();

        Assert.Equal(8, proxy.Calls.Count);
        Assert.Equal(8, proxy.Calls.Distinct().Count());
    }

    [Fact]
    public async Task Index_HoldsTheSectorsSnapshot_OpeningOnTheLatestYear()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.False(model.Sectors.Unavailable);
        Assert.Same(Snapshot, model.Sectors.Snapshot);
        Assert.Equal(new DateOnly(2025, 12, 31), model.Sectors.AsOf);
        Assert.Equal("2025", model.Sectors.LatestPeriod!.Period);
    }

    [Fact]
    public async Task Index_ReadsTheSectorsSnapshotOnce_AndOnlyTheCompaniesTabAndTheSectorsBasketFetchMore_SoTheChartNeedsNoFurtherRoundTrip()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        // The view embeds the whole snapshot as JSON for the chart, so the page makes no API call of its own.
        Assert.Equal([nameof(IResearchApi.GetSectorsAsync), nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetWorldAsync),
                nameof(IResearchApi.GetFundOverlapAsync), nameof(IResearchApi.GetFundReturnsAsync), nameof(IResearchApi.GetMacroAsync),
                nameof(IResearchApi.GetFundFactsAsync), nameof(IResearchApi.GetFundExposureAsync)],
            proxy.Calls);
        Assert.Same(Snapshot, model.Sectors.Snapshot);
        // The only other actions are the Companies tab's search, lookup and press releases, and the Sectors basket view's
        // data, which it fetches when first shown so the page does not carry it.
        Assert.Equal(
            [
                nameof(ResearchController.Company), nameof(ResearchController.FundIndustries), nameof(ResearchController.Home),
                nameof(ResearchController.Index),
                nameof(ResearchController.Releases), nameof(ResearchController.SearchIndex), nameof(ResearchController.Sectors),
                nameof(ResearchController.Suggest),
            ],
            typeof(ResearchController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Index_WhenTheSectorsDataHasNeverLoaded_MarksTheTabUnavailable()
    {
        string body = JsonSerializer.Serialize(new ResearchUnavailableDto { Reason = "The Sectors data has not loaded yet." },
            JsonSerializerOptions.Web);
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, body));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Sectors.Unavailable);
        Assert.Null(model.Sectors.Snapshot);
        Assert.Null(model.Sectors.AsOf);
        Assert.Equal("Research", model.Sectors.UnavailableState.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.Sectors.UnavailableState.Heading));
    }

    [Fact]
    public async Task Index_WhenApiServiceIsUnreachable_MarksTheTabUnavailable_WithoutExceptionDetails()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Sectors.Unavailable);
        Assert.DoesNotContain("unreachable", model.Sectors.UnavailableState.Heading, StringComparison.OrdinalIgnoreCase);
    }

    // Every dataset is read through one ReadAsync, so this proves its cancellation filter for all of them.
    [Fact]
    public async Task Index_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Index(cancellationToken: cts.Token));
    }

    private static readonly WorldSnapshotDto World = new()
    {
        AsOf = new DateOnly(2026, 10, 8),
        Periods = ["2024"],
        Countries = [new WorldCountryDto { Id = "840", Iso3 = "USA", Name = "United States" }],
        Values = [new WorldPeriodDto { Period = "2024", Countries = [new WorldCountryValueDto { Country = 0, MarketCap = 5e13m, PercentOfGdp = 172.4m }] }],
    };

    [Fact]
    public async Task Index_HoldsTheWorldSnapshot()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);
        proxy.Returns(nameof(IResearchApi.GetWorldAsync), World);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.False(model.World.Unavailable);
        Assert.Same(World, model.World.Snapshot);
        Assert.Equal(new DateOnly(2026, 10, 8), model.World.AsOf);
    }

    [Fact]
    public async Task Index_WhenTheWorldDataHasNeverLoaded_MarksOnlyTheWorldTabUnavailable()
    {
        string body = JsonSerializer.Serialize(new ResearchUnavailableDto { Reason = "The World data has not loaded yet." },
            JsonSerializerOptions.Web);
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);
        proxy.Throws(nameof(IResearchApi.GetWorldAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, body));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.World.Unavailable);
        Assert.Null(model.World.AsOf);
        Assert.Equal("Research", model.World.UnavailableState.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.World.UnavailableState.Heading));
        Assert.False(model.Sectors.Unavailable);
    }

    [Fact]
    public async Task Index_WhenOnlyTheSectorsDataFails_KeepsTheWorldAndFundsTabs()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));
        proxy.Returns(nameof(IResearchApi.GetWorldAsync), World);
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), new FundsSnapshotDto
        {
            AsOf = new DateOnly(2026, 6, 30),
            Periods = ["2026 Q2"],
            Funds = [new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" }],
            Values = [new FundsPeriodDto { Period = "2026 Q2", Funds = [new FundHoldingsDto { Ticker = "SPY", HoldingCount = 504 }] }],
        });

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Sectors.Unavailable);
        Assert.False(model.World.Unavailable);
        Assert.False(model.Funds.Unavailable);
    }

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

        IActionResult result = await CreateController(api).Home();

        Assert.IsType<ViewResult>(result);
        Assert.Equal([nameof(IResearchApi.GetStatusAsync)], proxy.Calls);
    }

    [Fact]
    public async Task Home_ListsEachDatasetWithItsAsOf_AndNotLoadedForAMissingOne()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), Status);

        ResearchHomeViewModel model = HomeModelOf(await CreateController(api).Home());

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

        ResearchHomeViewModel model = HomeModelOf(await CreateController(api).Home());

        Assert.Equal(8, model.Datasets.Count);
        Assert.All(model.Datasets, d => Assert.Null(d.AsOf));
        Assert.Equal(4, model.Presets.Count);
    }

    [Fact]
    public async Task Home_ReadsTheStatusThroughTheSnapshotCache_AndReadsAgainAfterAFailure()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        ResearchController controller = CreateController(api);

        ResearchHomeViewModel failed = HomeModelOf(await controller.Home());
        proxy.Returns(nameof(IResearchApi.GetStatusAsync), Status);
        ResearchHomeViewModel first = HomeModelOf(await controller.Home());
        ResearchHomeViewModel second = HomeModelOf(await controller.Home());

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

    [Theory]
    [InlineData("Technology", "Technology")]
    [InlineData(" Finance / National Commercial Banks ", "Finance/National Commercial Banks")]
    [InlineData("A/B/C", null)]
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
}
