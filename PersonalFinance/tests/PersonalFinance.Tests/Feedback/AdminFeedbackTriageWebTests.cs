using System.Net;
using System.Reflection;
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
using PersonalFinance.Web.Features.Feedback;
using PersonalFinance.Web.Models;
using Refit;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Light checks of Feedback Status triage on the Web <see cref="AdminController"/>: the inbox filter is passed to
/// <see cref="IFeedbackApi"/>, and a status change posts back to the inbox with a message.
/// </summary>
public class AdminFeedbackTriageWebTests
{
    private static AdminController CreateController(IFeedbackApi feedbackApi)
    {
        (IAdminApi? adminApi, ApiProxy<IAdminApi> _) = ApiProxy.Create<IAdminApi>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user")], "TestAuthType");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new AdminController(adminApi, feedbackApi, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FeedbackStatus.Archived)]
    public async Task Feedback_PassesTheStatusFilterToTheApi_AndShowsIt(FeedbackStatus? status)
    {
        var api = new FakeFeedbackApi();

        IActionResult result = await CreateController(api).Feedback(status);

        Assert.Equal([status], api.ListedStatuses);
        AdminFeedbackInboxViewModel model = Assert.IsType<AdminFeedbackInboxViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(status, model.Status);
    }

    [Fact]
    public async Task ChangeFeedbackStatus_ChangesIt_AndReturnsToTheSameInboxFilter()
    {
        var api = new FakeFeedbackApi();
        AdminController controller = CreateController(api);

        IActionResult result = await controller.ChangeFeedbackStatus(7, FeedbackStatus.Archived, FeedbackStatus.Reviewed);

        Assert.Equal([(7, FeedbackStatus.Archived)], api.Changes);
        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Feedback), redirect.ActionName);
        Assert.Equal(FeedbackStatus.Reviewed, redirect.RouteValues?["status"]);
        Assert.Equal("Feedback marked Archived.", controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task ChangeFeedbackStatus_UnknownFeedback_ShowsAFriendlyError()
    {
        var api = new FakeFeedbackApi { ChangeFailure = await ApiFailureAsync(HttpStatusCode.NotFound) };
        AdminController controller = CreateController(api);

        IActionResult result = await controller.ChangeFeedbackStatus(7, FeedbackStatus.Reviewed, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("That Feedback no longer exists.", controller.TempData["AdminError"]);
        Assert.Null(controller.TempData["AdminMessage"]);
    }

    [Fact]
    public async Task ChangeFeedbackStatus_WhenTheApiIsUnreachable_ShowsAFriendlyError()
    {
        var api = new FakeFeedbackApi { ChangeFailure = new HttpRequestException("ApiService unreachable") };
        AdminController controller = CreateController(api);

        await controller.ChangeFeedbackStatus(7, FeedbackStatus.Reviewed, null);

        string error = Assert.IsType<string>(controller.TempData["AdminError"]);
        Assert.DoesNotContain("unreachable", error);
    }

    [Fact]
    public void ChangeFeedbackStatus_IsAPost_WithAnAntiForgeryToken()
    {
        MethodInfo method = typeof(AdminController).GetMethod(nameof(AdminController.ChangeFeedbackStatus))!;

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private static async Task<ApiException> ApiFailureAsync(HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/feedback/7/status");
        using var response = new HttpResponseMessage(status) { RequestMessage = request };
        return await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings());
    }

    private sealed class FakeFeedbackApi : IFeedbackApi
    {
        public List<FeedbackStatus?> ListedStatuses { get; } = new();

        public List<(int Id, FeedbackStatus Status)> Changes { get; } = new();

        public Exception? ChangeFailure { get; init; }

        public Task SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            ListedStatuses.Add(status);
            return Task.FromResult(new List<FeedbackAdminItemDto>());
        }

        public Task ChangeStatusAsync(int id, ChangeFeedbackStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ChangeFailure is not null)
            {
                return Task.FromException(ChangeFailure);
            }

            Changes.Add((id, request.Status));
            return Task.CompletedTask;
        }
    }
}
