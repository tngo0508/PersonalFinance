using System.Net;
using System.Text;
using System.Text.Json;
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
/// The Web Research data the pages' scripts fetch: the Sectors basket view's fund industries and the search index, each
/// read through <see cref="IResearchApi"/> and the snapshot cache.
/// </summary>
public class ResearchFundsWebControllerTests
{
    private static ResearchController CreateController(IResearchApi api) =>
        new(api, TestResearchCache.Create(), NullLogger<ResearchController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

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
    public async Task FundIndustries_ReturnsApiServicesSnapshot_AsJson_ForTheSectorsBasketView()
    {
        // Fetched by the Sectors page's basket view when first shown, not read for the page (the page never asks for it).
        var industries = new FundIndustriesSnapshotDto { AsOf = new DateOnly(2026, 6, 30) };
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetFundIndustriesAsync), industries);

        IActionResult result = await CreateController(api).FundIndustries();

        Assert.Same(industries, Assert.IsType<JsonResult>(result).Value);
    }

    private static ResearchSearchIndexDto CompleteIndex() => new()
    {
        Funds = [new TrackedFundDto { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust" }],
        Offices = ["Technology"],
        Industries = [new ResearchSearchIndustryDto { Name = "Semiconductors", Office = "Technology" }],
        Countries = ["Brazil"],
    };

    [Fact]
    public async Task SearchIndex_ReturnsTheIndexAsJson_ReadingApiServiceOnceThroughTheCache()
    {
        ResearchSearchIndexDto index = CompleteIndex();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSearchIndexAsync), index);
        ResearchController controller = CreateController(api);

        IActionResult first = await controller.SearchIndex();
        IActionResult second = await controller.SearchIndex();

        Assert.Same(index, Assert.IsType<JsonResult>(first).Value);
        Assert.Same(index, Assert.IsType<JsonResult>(second).Value);
        Assert.Equal([nameof(IResearchApi.GetSearchIndexAsync)], proxy.Calls);
    }

    [Fact]
    public async Task SearchIndex_WhenAPartIsEmpty_ReadsApiServiceAgainOnTheNextRequest()
    {
        ResearchSearchIndexDto complete = CompleteIndex();
        ResearchSearchIndexDto partial = new() { Countries = ["Brazil"] };
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSearchIndexAsync), partial);
        ResearchController controller = CreateController(api);

        IActionResult first = await controller.SearchIndex();
        proxy.Returns(nameof(IResearchApi.GetSearchIndexAsync), complete);
        IActionResult second = await controller.SearchIndex();
        IActionResult third = await controller.SearchIndex();

        Assert.Same(partial, Assert.IsType<JsonResult>(first).Value);
        Assert.Same(complete, Assert.IsType<JsonResult>(second).Value);
        Assert.Same(complete, Assert.IsType<JsonResult>(third).Value);
        Assert.Equal(2, proxy.Calls.Count);
    }

    [Fact]
    public async Task SearchIndex_WhenApiServiceFails_KeepsTheStatusWithASafeMessage()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSearchIndexAsync), await NeverLoaded("secret internal reason"));
        ResearchController controller = CreateController(api);

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.SearchIndex());

        ObjectResult error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.DoesNotContain("secret internal reason", JsonSerializer.Serialize(error.Value));
        Assert.False(string.IsNullOrWhiteSpace(JsonSerializer.Serialize(error.Value)));
    }
}
