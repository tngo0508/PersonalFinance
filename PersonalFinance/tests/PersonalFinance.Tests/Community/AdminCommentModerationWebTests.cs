using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Admin;
using Refit;

namespace PersonalFinance.Tests.Community;

/// <summary>
/// The Comment moderation list on the Web <see cref="AdminController"/>: it shows what <see cref="ICommunityApi"/>
/// lists, hiding or showing a Comment posts the decision and returns with a message, and failures show a friendly
/// error rather than an empty list.
/// </summary>
public class AdminCommentModerationWebTests
{
    [Fact]
    public async Task Comments_ListsWhatTheApiReturns()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListCommentsForReviewAsync), new List<CommunityCommentReviewItemDto>
        {
            new() { Id = 3, Text = "Saved $400", HasAmount = true, IsHidden = true },
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Comments());

        CommunityCommentReviewItemDto item = Assert.Single(Assert.IsType<AdminCommentListViewModel>(view.Model).Items);
        Assert.True(item.HasAmount);
        Assert.True(item.IsHidden);
    }

    [Fact]
    public async Task Comments_WhenTheApiIsUnreachable_ShowsTheUnavailableState()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Comments());

        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        Assert.Equal("Comments", Assert.IsType<UnavailableStateViewModel>(view.Model).RetryAction);
    }

    [Theory]
    [InlineData(true, "Comment hidden.")]
    [InlineData(false, "Comment shown again.")]
    public async Task SetCommentVisibility_SendsTheDecision_AndReturnsToTheList(bool hidden, string message)
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.SetCommentVisibilityAsync), null);
        AdminController controller = CreateController(api);

        IActionResult result = await controller.SetCommentVisibility(7, hidden);

        Assert.Equal(7, proxy.Arguments.Single()[0]);
        Assert.Equal(hidden, Assert.IsType<CommunityCommentVisibilityRequestDto>(proxy.Arguments.Single()[1]).IsHidden);
        Assert.Equal(nameof(AdminController.Comments), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(message, controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task SetCommentVisibility_UnknownComment_ShowsAFriendlyError()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        using var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/community/comments/7/visibility");
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        proxy.Throws(nameof(ICommunityApi.SetCommentVisibilityAsync),
            await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings()));
        AdminController controller = CreateController(api);

        await controller.SetCommentVisibility(7, hidden: true);

        Assert.Equal("That Comment no longer exists.", controller.TempData["AdminError"]);
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
