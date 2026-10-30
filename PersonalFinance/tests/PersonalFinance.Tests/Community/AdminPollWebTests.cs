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
/// The Admin Poll page on the Web <see cref="AdminController"/>: it lists what <see cref="ICommunityApi"/> returns,
/// writing a Poll sends only the filled-in options and keeps a rejected form as typed, opening or closing posts the
/// decision and returns with a message, and failures show a friendly error rather than an empty list.
/// </summary>
public class AdminPollWebTests
{
    [Fact]
    public async Task Polls_ListsWhatTheApiReturns_WithAnEmptyForm()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListPollsAsync), new List<CommunityPollAdminItemDto>
        {
            new() { Id = 3, Question = "Which habit stuck?", IsOpen = true, VoteCount = 12 },
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Polls());

        AdminPollListViewModel model = Assert.IsType<AdminPollListViewModel>(view.Model);
        Assert.Equal(12, Assert.Single(model.Items).VoteCount);
        Assert.Equal(string.Empty, model.Form.Question);
    }

    [Fact]
    public async Task Polls_WhenTheApiIsUnreachable_ShowsTheUnavailableState()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Polls());

        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        Assert.Equal("Polls", Assert.IsType<UnavailableStateViewModel>(view.Model).RetryAction);
    }

    [Fact]
    public async Task CreatePoll_SendsTheFilledOptionsTrimmed_AndReturnsToTheList()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.CreatePollAsync), new CommunityPollAdminItemDto { Id = 4 });
        AdminController controller = CreateController(api);

        IActionResult result = await controller.CreatePoll(new AdminPollForm
        {
            Question = "Which habit stuck?",
            Category = "Habits",
            Options = [" Meal planning ", null, "  ", "Packed lunches", null, null],
        });

        CommunityPollRequestDto sent = Assert.IsType<CommunityPollRequestDto>(proxy.Arguments.Single()[0]);
        Assert.Equal(["Meal planning", "Packed lunches"], sent.Options);
        Assert.Equal("Habits", sent.Category);
        Assert.Equal(nameof(AdminController.Polls), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("Poll saved. It stays closed until you open it.", controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task CreatePoll_WithFewerThanTwoOptions_ShowsTheFormAgain_WithoutCallingTheApiToWrite()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListPollsAsync), new List<CommunityPollAdminItemDto>());
        AdminController controller = CreateController(api);
        var form = new AdminPollForm { Question = "Which habit stuck?", Category = "Habits", Options = ["Only one"] };

        ViewResult view = Assert.IsType<ViewResult>(await controller.CreatePoll(form));

        Assert.Equal(nameof(AdminController.Polls), view.ViewName);
        Assert.Same(form, Assert.IsType<AdminPollListViewModel>(view.Model).Form);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal([nameof(ICommunityApi.ListPollsAsync)], proxy.Calls);
    }

    [Fact]
    public async Task CreatePoll_RejectedByTheApi_ShowsTheFormAgain_WithTheReason()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListPollsAsync), new List<CommunityPollAdminItemDto>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/community/polls");
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = request };
        proxy.Throws(nameof(ICommunityApi.CreatePollAsync),
            await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings()));
        AdminController controller = CreateController(api);
        var form = new AdminPollForm { Question = "Which habit stuck?", Category = "Goals", Options = ["Yes", "No"] };

        ViewResult view = Assert.IsType<ViewResult>(await controller.CreatePoll(form));

        Assert.Same(form, Assert.IsType<AdminPollListViewModel>(view.Model).Form);
        Assert.NotEmpty(Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(true, "Poll opened.")]
    [InlineData(false, "Poll closed.")]
    public async Task SetPollStatus_SendsTheDecision_AndReturnsToTheList(bool open, string message)
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.SetPollStatusAsync), null);
        AdminController controller = CreateController(api);

        IActionResult result = await controller.SetPollStatus(7, open);

        Assert.Equal(7, proxy.Arguments.Single()[0]);
        Assert.Equal(open, Assert.IsType<CommunityPollStatusRequestDto>(proxy.Arguments.Single()[1]).IsOpen);
        Assert.Equal(nameof(AdminController.Polls), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(message, controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task SetPollStatus_UnknownPoll_ShowsAFriendlyError()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        using var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/community/polls/7/status");
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        proxy.Throws(nameof(ICommunityApi.SetPollStatusAsync),
            await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings()));
        AdminController controller = CreateController(api);

        await controller.SetPollStatus(7, open: true);

        Assert.Equal("That Poll no longer exists.", controller.TempData["AdminError"]);
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
