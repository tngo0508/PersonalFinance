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
/// The Admin Challenge page on the Web <see cref="AdminController"/>: it lists what <see cref="ICommunityApi"/>
/// returns, writing a Challenge sends the form and returns with a message, a rejected form is kept as typed, and a
/// failed load shows the Unavailable State rather than an empty list.
/// </summary>
public class AdminChallengeWebTests
{
    [Fact]
    public async Task Challenges_ListsWhatTheApiReturns_WithAnEmptyForm()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListChallengesAsync), new List<CommunityChallengeAdminItemDto>
        {
            new() { Id = 3, Title = "Cook at home", MemberCount = 12 },
        });

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Challenges());

        AdminChallengeListViewModel model = Assert.IsType<AdminChallengeListViewModel>(view.Model);
        Assert.Equal(12, Assert.Single(model.Items).MemberCount);
        Assert.Equal(string.Empty, model.Form.Title);
        Assert.Null(model.Form.StartDate);
    }

    [Fact]
    public async Task Challenges_WhenTheApiIsUnreachable_ShowsTheUnavailableState()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> _) = ApiProxy.Create<ICommunityApi>();

        ViewResult view = Assert.IsType<ViewResult>(await CreateController(api).Challenges());

        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        Assert.Equal("Challenges", Assert.IsType<UnavailableStateViewModel>(view.Model).RetryAction);
    }

    [Fact]
    public async Task CreateChallenge_SendsTheForm_AndReturnsToTheList()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.CreateChallengeAsync), new CommunityChallengeAdminItemDto { Id = 4 });
        AdminController controller = CreateController(api);

        IActionResult result = await controller.CreateChallenge(Form());

        CommunityChallengeRequestDto sent = Assert.IsType<CommunityChallengeRequestDto>(proxy.Arguments.Single()[0]);
        Assert.Equal("Cook at home", sent.Title);
        Assert.Equal(CommunityChallengePeriod.Weekly, sent.Period);
        Assert.Equal(new DateOnly(2026, 11, 1), sent.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 30), sent.EndDate);
        Assert.Equal(nameof(AdminController.Challenges), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("Challenge saved.", controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task CreateChallenge_WithAnInvalidForm_ShowsItAgain_WithoutCallingTheApiToWrite()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListChallengesAsync), new List<CommunityChallengeAdminItemDto>());
        AdminController controller = CreateController(api);
        controller.ModelState.AddModelError("Form.StartDate", "Choose a start date.");
        AdminChallengeForm form = Form();

        ViewResult view = Assert.IsType<ViewResult>(await controller.CreateChallenge(form));

        Assert.Equal(nameof(AdminController.Challenges), view.ViewName);
        Assert.Same(form, Assert.IsType<AdminChallengeListViewModel>(view.Model).Form);
        Assert.Equal([nameof(ICommunityApi.ListChallengesAsync)], proxy.Calls);
    }

    [Fact]
    public async Task CreateChallenge_RejectedByTheApi_ShowsTheFormAgain_WithTheReason()
    {
        (ICommunityApi api, ApiProxy<ICommunityApi> proxy) = ApiProxy.Create<ICommunityApi>();
        proxy.Returns(nameof(ICommunityApi.ListChallengesAsync), new List<CommunityChallengeAdminItemDto>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/community/challenges");
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = request };
        proxy.Throws(nameof(ICommunityApi.CreateChallengeAsync),
            await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings()));
        AdminController controller = CreateController(api);
        AdminChallengeForm form = Form();

        ViewResult view = Assert.IsType<ViewResult>(await controller.CreateChallenge(form));

        Assert.Same(form, Assert.IsType<AdminChallengeListViewModel>(view.Model).Form);
        Assert.NotEmpty(Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    private static AdminChallengeForm Form() => new()
    {
        Title = "Cook at home",
        Habit = "Cook dinner at home instead of ordering in.",
        Period = CommunityChallengePeriod.Weekly,
        StartDate = new DateOnly(2026, 11, 1),
        EndDate = new DateOnly(2026, 11, 30),
    };

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
