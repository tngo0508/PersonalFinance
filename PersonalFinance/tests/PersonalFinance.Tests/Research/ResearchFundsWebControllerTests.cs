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
/// The Web Research page's Funds tab: it reads the funds snapshot through <see cref="IResearchApi"/>, and each tab
/// falls back to its own Unavailable State, so the Sectors tab keeps working when the funds data has never loaded.
/// </summary>
public class ResearchFundsWebControllerTests
{
    private static readonly SectorsSnapshotDto Sectors = new()
    {
        AsOf = new DateOnly(2025, 6, 30),
        Periods = ["2025"],
        Values = [new SectorsPeriodDto { Period = "2025" }],
    };

    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2026 Q1", "2026 Q2"],
        Funds = [new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" }],
        Values =
        [
            new FundsPeriodDto { Period = "2026 Q1", Funds = [new FundHoldingsDto { Ticker = "SPY", HoldingCount = 503 }] },
            new FundsPeriodDto { Period = "2026 Q2", Funds = [new FundHoldingsDto { Ticker = "SPY", HoldingCount = 504 }] },
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
        return await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/funds"),
            HttpMethod.Get, response, new RefitSettings());
    }

    [Fact]
    public async Task Index_HoldsTheFundsSnapshot_OpeningOnTheLatestQuarter()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.False(model.Funds.Unavailable);
        Assert.Same(Funds, model.Funds.Snapshot);
        Assert.Equal(new DateOnly(2026, 6, 30), model.Funds.AsOf);
        Assert.Equal("2026 Q2", model.Funds.LatestPeriod!.Period);
    }

    [Fact]
    public async Task Index_WhenTheFundsDataHasNeverLoaded_MarksOnlyTheFundsTabUnavailable()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Throws(nameof(IResearchApi.GetFundsAsync), await NeverLoaded("The funds data has not loaded yet."));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Funds.Unavailable);
        Assert.Null(model.Funds.AsOf);
        Assert.Equal("Research", model.Funds.UnavailableState.RetryController);
        Assert.False(model.Sectors.Unavailable);
        Assert.Same(Sectors, model.Sectors.Snapshot);
    }

    [Fact]
    public async Task Index_HoldsTheFundOverlap_ForTheFundsTab()
    {
        var overlap = new FundOverlapSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds);
        proxy.Returns(nameof(IResearchApi.GetFundOverlapAsync), overlap);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.Same(overlap, model.FundOverlap);
    }

    [Fact]
    public async Task Index_WhenTheFundOverlapHasNeverLoaded_StillHoldsTheFunds_AndRetriesOnTheFundsTab()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds);
        proxy.Throws(nameof(IResearchApi.GetFundOverlapAsync), await NeverLoaded("The fund overlap data has not loaded yet."));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.Null(model.FundOverlap);
        Assert.False(model.Funds.Unavailable);
        Assert.Equal("funds", model.FundOverlapUnavailableState.RetryRouteValues["tab"]);
    }

    [Fact]
    public async Task Index_WhenTheSectorsDataHasNeverLoaded_StillHoldsTheFunds()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await NeverLoaded("The Sectors data has not loaded yet."));
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.Sectors.Unavailable);
        Assert.False(model.Funds.Unavailable);
    }

    [Fact]
    public async Task Index_WhenTheRequestIsCancelledDuringTheFundsRead_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);
        proxy.Throws(nameof(IResearchApi.GetFundsAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Index(cancellationToken: cts.Token));
    }
}
