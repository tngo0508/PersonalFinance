using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.Budget;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Light checks of the Admin Feedback inbox page on the Web <see cref="AdminController"/>: it shows what
/// <see cref="IFeedbackApi"/> returns, and a failed load is the Unavailable State, never an empty inbox.
/// </summary>
public class AdminFeedbackInboxWebTests
{
    private static AdminController CreateController(IFeedbackApi feedbackApi)
    {
        var (adminApi, _) = ApiProxy.Create<IAdminApi>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user")], "TestAuthType");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new AdminController(adminApi, feedbackApi, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    [Fact]
    public async Task Feedback_ShowsTheInboxItems()
    {
        var (api, proxy) = ApiProxy.Create<IFeedbackApi>();
        proxy.Returns(nameof(IFeedbackApi.ListAsync), new List<FeedbackAdminItemDto>
        {
            new() { Id = 2, Category = FeedbackCategory.Bug, Message = "Blank chart", AccountEmail = "a@example.com" },
            new() { Id = 1, Category = FeedbackCategory.Idea, Message = "Dark mode", AccountEmail = "b@example.com" }
        });

        var result = await CreateController(api).Feedback();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminFeedbackInboxViewModel>(view.Model);
        Assert.Equal(["Blank chart", "Dark mode"], model.Items.Select(i => i.Message));
    }

    [Fact]
    public async Task Feedback_WhenThereIsNone_ShowsAnEmptyInbox()
    {
        var (api, proxy) = ApiProxy.Create<IFeedbackApi>();
        proxy.Returns(nameof(IFeedbackApi.ListAsync), new List<FeedbackAdminItemDto>());

        var result = await CreateController(api).Feedback();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsType<AdminFeedbackInboxViewModel>(view.Model).Items);
    }

    [Fact]
    public async Task Feedback_WhenTheApiFails_ShowsTheUnavailableState_ThatRetriesTheInbox()
    {
        var (api, _) = ApiProxy.Create<IFeedbackApi>();

        var result = await CreateController(api).Feedback();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(UnavailableStateExtensions.UnavailableViewName, view.ViewName);
        var model = Assert.IsType<UnavailableStateViewModel>(view.Model);
        Assert.Equal("Admin", model.RetryController);
        Assert.Equal(nameof(AdminController.Feedback), model.RetryAction);
        Assert.DoesNotContain("ApiService unreachable", model.Heading);
    }

    [Fact]
    public async Task Feedback_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var (api, proxy) = ApiProxy.Create<IFeedbackApi>();
        proxy.Throws(nameof(IFeedbackApi.ListAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Feedback(cancellationToken: cts.Token));
    }
}
