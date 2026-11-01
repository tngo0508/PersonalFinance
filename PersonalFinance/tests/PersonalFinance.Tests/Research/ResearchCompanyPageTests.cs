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
/// The Research Company page: it reads no snapshot for an empty search or a rejected ticker, passes only a checked query
/// to the view, answers 404 for a rejected ticker, and sends a Tracked Fund's ticker to its Fund page with the basket.
/// </summary>
public class ResearchCompanyPageTests
{
    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds = [new TrackedFundDto { Ticker = "VTI", Name = "Vanguard Total Stock Market ETF" }],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchCompanyPageViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ResearchCompanyPageViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static (IResearchApi Api, ApiProxy<IResearchApi> Proxy) WithFunds()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetFundsAsync), Funds);
        return (api, proxy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<script>")]
    public async Task Company_WithoutAValidTicker_ReadsNoSnapshot(string? ticker)
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = WithFunds();

        await CreateController(api).Company(ticker);

        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public async Task Company_WithoutATicker_OpensOnTheEmptySearch()
    {
        (IResearchApi api, _) = WithFunds();

        ResearchCompanyPageViewModel model = ModelOf(await CreateController(api).Company(null));

        Assert.Null(model.Ticker);
        Assert.Null(model.Query);
    }

    [Theory]
    [InlineData("  microsoft  ", "microsoft")]
    [InlineData("   ", null)]
    [InlineData("a query far longer than the forty characters allowed", null)]
    public async Task Company_WithAQuery_PassesTheNormalizedQueryToTheView(string q, string? expected)
    {
        (IResearchApi api, _) = WithFunds();

        ResearchCompanyPageViewModel model = ModelOf(await CreateController(api).Company(null, q));

        Assert.Equal(expected, model.Query);
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("WAY-TOO-LONG-TICKER")]
    public async Task Company_WithARejectedTicker_Returns404(string ticker)
    {
        (IResearchApi api, _) = WithFunds();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Company(ticker));

        Assert.Equal(("NotFound", StatusCodes.Status404NotFound), (view.ViewName, view.StatusCode));
        Assert.True(Assert.IsType<ResearchNotFoundViewModel>(view.Model).IsCompany);
    }

    [Fact]
    public async Task Company_ForACompanyTicker_OpensItsLookup()
    {
        (IResearchApi api, _) = WithFunds();

        ResearchCompanyPageViewModel model = ModelOf(await CreateController(api).Company("brk.b"));

        Assert.Equal("BRK-B", model.Ticker);
    }

    [Theory]
    [InlineData("vti", "VTI:60,SPY:40", "VTI:60,SPY:40")]
    [InlineData("VTI", "<script>", null)]
    [InlineData("VTI", null, null)]
    public async Task Company_ForATrackedFundTicker_RedirectsToTheFundPage(string ticker, string? basket, string? keptBasket)
    {
        (IResearchApi api, _) = WithFunds();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(await CreateController(api).Company(ticker, basket: basket));

        Assert.Equal(nameof(ResearchController.Fund), redirect.ActionName);
        Assert.Equal("VTI", redirect.RouteValues!["ticker"]);
        Assert.Equal(keptBasket, redirect.RouteValues["basket"]);
        Assert.False(redirect.Permanent);
    }

    [Fact]
    public async Task Company_WhenTheFundsDataCannotBeRead_StillOpensTheLookup()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetFundsAsync), await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/funds"), HttpMethod.Get,
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}", Encoding.UTF8, "application/json") },
            new RefitSettings()));

        ResearchCompanyPageViewModel model = ModelOf(await CreateController(api).Company("VTI"));

        Assert.Equal("VTI", model.Ticker);
    }
}
