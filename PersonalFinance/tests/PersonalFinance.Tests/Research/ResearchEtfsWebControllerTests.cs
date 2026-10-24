using System.Net;
using System.Text;
using System.Text.Json;
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
/// The Web Research page's ETFs tab: it reads the ETFs snapshot through <see cref="IResearchApi"/>, and each tab
/// falls back to its own Unavailable State, so the Sectors tab keeps working when the ETFs data has never loaded.
/// </summary>
public class ResearchEtfsWebControllerTests
{
    private static readonly SectorsSnapshotDto Sectors = new()
    {
        AsOf = new DateOnly(2025, 6, 30),
        Periods = ["2025"],
        Values = [new SectorsPeriodDto { Period = "2025" }],
    };

    private static readonly EtfsSnapshotDto Etfs = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q1", "2026 Q2"],
        Funds = [new EtfFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" }],
        Values =
        [
            new EtfsPeriodDto { Period = "2026 Q1", Funds = [new EtfHoldingsDto { Ticker = "SPY", HoldingCount = 503 }] },
            new EtfsPeriodDto { Period = "2026 Q2", Funds = [new EtfHoldingsDto { Ticker = "SPY", HoldingCount = 504 }] },
        ],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, NullLogger<ResearchController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<ResearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<ApiException> NeverLoaded(string reason)
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(JsonSerializer.Serialize(new ResearchUnavailableDto { Reason = reason },
                JsonSerializerOptions.Web), Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/etfs"),
            HttpMethod.Get, response, new RefitSettings());
    }

    [Fact]
    public async Task Index_HoldsTheEtfsSnapshot_OpeningOnTheLatestQuarter()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Returns(nameof(IResearchApi.GetEtfsAsync), Etfs);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.False(model.EtfsUnavailable);
        Assert.Same(Etfs, model.Etfs);
        Assert.Equal(new DateOnly(2026, 6, 30), model.EtfsAsOf);
        Assert.Equal("2026 Q2", model.EtfsLatestPeriod!.Period);
    }

    [Fact]
    public async Task Index_WhenTheEtfsDataHasNeverLoaded_MarksOnlyTheEtfsTabUnavailable()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Throws(nameof(IResearchApi.GetEtfsAsync), await NeverLoaded("The ETFs data has not loaded yet."));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.EtfsUnavailable);
        Assert.Null(model.EtfsAsOf);
        Assert.Equal("Research", model.EtfsUnavailableState.RetryController);
        Assert.False(model.SectorsUnavailable);
        Assert.Same(Sectors, model.Sectors);
    }

    [Fact]
    public async Task Index_WhenTheSectorsDataHasNeverLoaded_StillHoldsTheEtfs()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await NeverLoaded("The Sectors data has not loaded yet."));
        proxy.Returns(nameof(IResearchApi.GetEtfsAsync), Etfs);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.SectorsUnavailable);
        Assert.False(model.EtfsUnavailable);
    }

    [Fact]
    public async Task Index_WhenTheRequestIsCancelledDuringTheEtfsRead_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Throws(nameof(IResearchApi.GetEtfsAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Index(cts.Token));
    }
}
