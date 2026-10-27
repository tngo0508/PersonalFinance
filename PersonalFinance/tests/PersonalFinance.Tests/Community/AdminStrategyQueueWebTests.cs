using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Admin;
using Refit;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Strategy review queue on the Web <see cref="AdminController"/>: the status filter is passed to
/// <see cref="ICommunityApi"/>, a decision posts back to the same filter with a message, and failures show a friendly
/// error rather than an empty queue.
/// </summary>
public class AdminStrategyQueueWebTests
{
    [Fact]
    public async Task Strategies_ListsTheChosenStatus()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListStrategiesForReviewAsync), new List<CommunityStrategyReviewItemDto>
        {
            new() { Id = 3, Title = "Saved $400", HasAmount = true },
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Strategies(CommunityStrategyStatus.Rejected));

        AdminStrategyQueueViewModel model = Assert.IsType<AdminStrategyQueueViewModel>(view.Model);
        Assert.Equal(CommunityStrategyStatus.Rejected, model.Status);
        Assert.True(Assert.Single(model.Items).HasAmount);
        Assert.Equal(CommunityStrategyStatus.Rejected, proxy.Arguments.Single()[0]);
    }

    [Fact]
    public async Task Strategies_WhenTheApiIsUnreachable_ShowsTheUnavailableState()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Strategies());

        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        Assert.Equal("Strategies", Assert.IsType<UnavailableStateViewModel>(view.Model).RetryAction);
    }

    [Fact]
    public async Task ReviewStrategy_SendsTheDecision_AndReturnsToTheSameFilter()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ReviewStrategyAsync), null);
        AdminController controller = CreateController(api);

        IActionResult result = await controller.ReviewStrategy(7, CommunityStrategyStatus.Approved,
            CommunityStrategyStatus.Pending);

        Assert.Equal(7, proxy.Arguments.Single()[0]);
        Assert.Equal(CommunityStrategyStatus.Approved,
            Assert.IsType<CommunityStrategyReviewRequestDto>(proxy.Arguments.Single()[1]).Status);
        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Strategies), redirect.ActionName);
        Assert.Equal(CommunityStrategyStatus.Pending, redirect.RouteValues?["status"]);
        Assert.Equal("Strategy approved.", controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task ReviewStrategy_UnknownStrategy_ShowsAFriendlyError()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        using var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/community/strategies/7/status");
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        proxy.Throws(nameof(ICommunityApi.ReviewStrategyAsync),
            await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings()));
        AdminController controller = CreateController(api);

        await controller.ReviewStrategy(7, CommunityStrategyStatus.Rejected);

        Assert.Equal("That Strategy no longer exists.", controller.TempData["AdminError"]);
        Assert.Null(controller.TempData["AdminMessage"]);
    }

    private static AdminController CreateController(ICommunityApi communityApi)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user")], "TestAuthType");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new AdminController(ApiProxy.Create<IAdminApi>().Api, ApiProxy.Create<IFeedbackApi>().Api, communityApi,
            NullLogger<AdminController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }
}
