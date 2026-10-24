using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
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
        new(api, NullLogger<ResearchController>.Instance)
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
    public void Controller_RequiresSignIn()
    {
        Assert.NotNull(typeof(ResearchController).GetCustomAttribute<AuthorizeAttribute>());
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
    public async Task Index_ReadsTheSectorsSnapshotOnce_AndOnlyTheCompaniesTabFetchesMore_SoTheSunburstNeedsNoFurtherRoundTrip()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        // The view embeds the whole snapshot as JSON for the sunburst, so the page makes no API call of its own.
        Assert.Equal([nameof(IResearchApi.GetSectorsAsync), nameof(IResearchApi.GetFundsAsync), nameof(IResearchApi.GetWorldAsync),
                nameof(IResearchApi.GetFundOverlapAsync), nameof(IResearchApi.GetFundReturnsAsync)],
            proxy.Calls);
        Assert.Same(Snapshot, model.Sectors.Snapshot);
        // The only other actions are the Companies tab's search and lookup, which no snapshot tab uses.
        Assert.Equal([nameof(ResearchController.Company), nameof(ResearchController.Index), nameof(ResearchController.Suggest)],
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
    public async Task Index_WhenOnlyTheSectorsDataFails_KeepsTheWorldTab()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, "{}"));
        proxy.Returns(nameof(IResearchApi.GetWorldAsync), World);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Sectors.Unavailable);
        Assert.False(model.World.Unavailable);
    }

    [Fact]
    public async Task Index_WhenTheWorldRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);
        proxy.Throws(nameof(IResearchApi.GetWorldAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Index(cancellationToken: cts.Token));
    }
}
