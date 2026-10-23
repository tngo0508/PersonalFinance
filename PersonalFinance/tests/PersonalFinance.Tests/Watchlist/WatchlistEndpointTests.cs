using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using ApiException = Refit.ApiException;
using RefitSettings = Refit.RefitSettings;
using WatchlistApiController = PersonalFinance.ApiService.Features.Watchlist.WatchlistController;
using WatchlistWebController = PersonalFinance.Web.Controllers.WatchlistController;

namespace PersonalFinance.Tests.Watchlist;

/// <summary>
/// The Watchlist endpoints: the ApiService controller is signed-in only and scopes every call to the Account in the
/// claims; the Web JSON controller forwards to it, needs the antiforgery token to write, and turns a failure into an
/// error status without exception details.
/// </summary>
public class WatchlistEndpointTests
{
    private const string UserId = "watchlist-user";

    private sealed class FakeWatchlistService : IWatchlistService
    {
        public List<string> UserIds { get; } = [];

        public Task<List<SavedArticleResponseDto>> GetWatchlistAsync(string userId, CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new List<SavedArticleResponseDto> { new() { Id = 1, Title = "Rates fall" } });
        }

        public Task<SaveArticleResultDto> SaveArticleAsync(string userId, SaveArticleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(new SaveArticleResultDto { Success = true, AlreadySaved = true, Id = 7 });
        }

        public Task<bool> RemoveArticleAsync(string userId, int id, CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(id == 1);
        }

        public Task<bool> IsArticleSavedAsync(string userId, string url, CancellationToken cancellationToken = default)
        {
            UserIds.Add(userId);
            return Task.FromResult(url == "https://example.test/saved");
        }
    }

    private static WatchlistApiController CreateApiController(IWatchlistService service)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "TestAuthType");
        return new WatchlistApiController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static object? Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);

    [Fact]
    public void ApiController_RequiresAuthentication()
    {
        Assert.NotNull(typeof(WatchlistApiController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task ApiController_ScopesEveryCallToTheAccountInTheClaims()
    {
        var service = new FakeWatchlistService();
        WatchlistApiController controller = CreateApiController(service);

        await controller.GetWatchlist();
        await controller.SaveArticle(new SaveArticleRequestDto { Url = "https://example.test/saved" });
        await controller.RemoveArticle(1);
        await controller.CheckArticleSaved("https://example.test/saved");

        Assert.Equal([UserId, UserId, UserId, UserId], service.UserIds);
    }

    [Fact]
    public async Task ApiController_Save_ReportsWhetherTheArticleWasAlreadySaved()
    {
        IActionResult result = await CreateApiController(new FakeWatchlistService())
            .SaveArticle(new SaveArticleRequestDto { Url = "https://example.test/saved" });

        object body = Assert.IsType<OkObjectResult>(result).Value!;
        Assert.Equal(true, Property(body, "alreadySaved"));
        Assert.Equal(7, Property(body, "id"));
    }

    [Fact]
    public async Task ApiController_RemovingAnArticleTheAccountDoesNotHave_ReturnsNotFound()
    {
        IActionResult result = await CreateApiController(new FakeWatchlistService()).RemoveArticle(404);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApiController_Check_ReportsWhetherTheUrlIsSaved()
    {
        IActionResult result = await CreateApiController(new FakeWatchlistService())
            .CheckArticleSaved("https://example.test/saved");

        Assert.Equal(true, Property(Assert.IsType<OkObjectResult>(result).Value!, "isSaved"));
    }

    [Fact]
    public void WebController_IsSignedInOnly_AnswersJsonErrors_AndNeedsTheAntiforgeryTokenToWrite()
    {
        Type controller = typeof(WatchlistWebController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<JsonApiErrorsAttribute>());
        Assert.NotNull(controller.GetMethod(nameof(WatchlistWebController.Save))!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(controller.GetMethod(nameof(WatchlistWebController.Remove))!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task WebController_RemovingAnUnknownArticle_PassesOnTheNotFound_WithoutExceptionDetails()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("System.InvalidOperationException: secret stack trace", Encoding.UTF8, "text/plain")
        };
        ApiException notFound = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Delete, "https://api.local/api/Watchlist/404"), HttpMethod.Delete, response,
            new RefitSettings());
        var controller = new WatchlistWebController(new FailingWatchlistApi(notFound))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Remove(404));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(objectResult.Value));
    }

    private sealed class FailingWatchlistApi(Exception failure) : IWatchlistApi
    {
        public Task<List<SavedArticleDto>> GetWatchlistAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<List<SavedArticleDto>>(failure);

        public Task<SaveArticleResponseDto> SaveArticleAsync(SaveArticleRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SaveArticleResponseDto>(failure);

        public Task<RemoveArticleResponseDto> RemoveArticleAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromException<RemoveArticleResponseDto>(failure);

        public Task<CheckArticleSavedResponseDto> CheckArticleSavedAsync(string url,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CheckArticleSavedResponseDto>(failure);
    }
}
