using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.News;
using Refit;

namespace PersonalFinance.Tests.Watchlist;

/// <summary>
/// The Web Finance News page reads the feed from ApiService through <see cref="INewsApi"/>. Filters ApiService
/// rejects find no articles. A failure shows a message on the page, or reaches the refresh script as an error status
/// through <see cref="JsonApiErrorsAttribute"/>, and never carries exception details.
/// </summary>
public class NewsWebControllerTests
{
    private sealed class FakeNewsApi : INewsApi
    {
        public Exception? Failure { get; init; }

        /// <summary>
        /// Answers any filtered query with 400 and the filter options in the body, as ApiService does for an overlong
        /// filter.
        /// </summary>
        public bool RejectFilters { get; init; }

        public List<NewsQueryDto> Queries { get; } = [];

        public NewsFeedDto Feed { get; } = new()
        {
            Articles = [new NewsArticleDto { Title = "Rates fall", Url = "https://example.test/rates", Category = "Investing" }],
            Sources = [new NewsSourceDto { Name = "BBC Business" }],
            Topics = ["Investing", "General"],
        };

        public async Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            if (Failure is not null)
            {
                throw Failure;
            }

            if (RejectFilters && (query.Category ?? query.Source ?? query.Search) is not null)
            {
                var rejected = new NewsFeedDto
                {
                    Errors = ["The length of 'Search' must be 100 characters or fewer."],
                    Sources = Feed.Sources,
                    Topics = Feed.Topics,
                };
                var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(JsonSerializer.Serialize(rejected, JsonSerializerOptions.Web),
                        Encoding.UTF8, "application/json")
                };
                throw await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/News"),
                    HttpMethod.Get, response, new RefitSettings());
            }

            return Feed;
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static NewsController CreateController(INewsApi api) =>
        new(api, new FixedTimeProvider(Now), NullLogger<NewsController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static NewsViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<NewsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static NewsRefreshResponse RefreshResponseOf(IActionResult result) =>
        Assert.IsType<NewsRefreshResponse>(Assert.IsType<JsonResult>(result).Value);

    [Fact]
    public async Task Index_BuildsTheViewModelFromTheFeed_AndForwardsTheFilters()
    {
        var api = new FakeNewsApi();
        var query = new NewsQueryDto { Category = "Investing", Source = "BBC Business", Search = "rates" };

        NewsViewModel model = ViewModelOf(await CreateController(api).Index(query));

        Assert.Equal("Rates fall", Assert.Single(model.Articles).Title);
        Assert.Equal("BBC Business", Assert.Single(model.Sources).Name);
        Assert.Equal(["Investing", "General"], model.Topics);
        Assert.Equal("Investing", model.SelectedCategory);
        Assert.Equal("BBC Business", model.SelectedSource);
        Assert.Equal("rates", model.SearchQuery);
        Assert.False(model.IsLoading);
        Assert.Equal(Now.UtcDateTime, model.LastRefreshed);
        Assert.Null(model.ErrorMessage);
        Assert.Same(query, Assert.Single(api.Queries));
    }

    [Fact]
    public async Task Index_WhenApiServiceRejectsTheFilters_ShowsNoArticles_WithTheFilterOptions()
    {
        var api = new FakeNewsApi { RejectFilters = true };

        NewsViewModel model = ViewModelOf(await CreateController(api).Index(new NewsQueryDto { Search = "too long" }));

        Assert.Empty(model.Articles);
        Assert.Null(model.ErrorMessage);
        Assert.Equal("BBC Business", Assert.Single(model.Sources).Name);
        Assert.Equal(["Investing", "General"], model.Topics);
        Assert.Equal("too long", model.SearchQuery);
        Assert.False(model.IsLoading);
        Assert.Single(api.Queries);
    }

    [Fact]
    public async Task Index_WhenApiServiceFails_ShowsAMessageWithoutExceptionDetails()
    {
        var api = new FakeNewsApi { Failure = new InvalidOperationException("secret stack trace") };

        NewsViewModel model = ViewModelOf(await CreateController(api).Index(new NewsQueryDto()));

        Assert.Empty(model.Articles);
        Assert.False(model.IsLoading);
        Assert.NotNull(model.ErrorMessage);
        Assert.DoesNotContain("secret", model.ErrorMessage);
    }

    [Fact]
    public async Task Refresh_ReturnsTheArticlesAsJson()
    {
        NewsRefreshResponse response =
            RefreshResponseOf(await CreateController(new FakeNewsApi()).Refresh(new NewsQueryDto { Search = "rates" }));

        Assert.True(response.Success);
        Assert.Equal("Rates fall", Assert.Single(response.Articles!).Title);
        Assert.Equal(Now.UtcDateTime, response.LastRefreshed);
    }

    [Fact]
    public async Task Refresh_WhenApiServiceRejectsTheFilters_ReturnsNoArticles()
    {
        var api = new FakeNewsApi { RejectFilters = true };

        NewsRefreshResponse response =
            RefreshResponseOf(await CreateController(api).Refresh(new NewsQueryDto { Search = "too long" }));

        Assert.True(response.Success);
        Assert.Empty(response.Articles!);
        Assert.Single(api.Queries);
    }

    [Fact]
    public void Refresh_IsAJsonActionWithApiErrors()
    {
        MethodInfo refresh = typeof(NewsController).GetMethod(nameof(NewsController.Refresh))!;

        Assert.NotNull(refresh.GetCustomAttribute<JsonApiErrorsAttribute>());
    }

    [Fact]
    public async Task Refresh_WhenApiServiceFails_ReturnsItsStatusWithoutExceptionDetails()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("System.InvalidOperationException: secret stack trace", Encoding.UTF8, "text/plain")
        };
        ApiException failure = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/News"), HttpMethod.Get, response,
            new RefitSettings());
        NewsController controller = CreateController(new FakeNewsApi { Failure = failure });

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Refresh(new NewsQueryDto()));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        string body = JsonSerializer.Serialize(objectResult.Value);
        Assert.Contains("error", body);
        Assert.DoesNotContain("secret", body);
    }

    [Fact]
    public async Task Refresh_WhenTheFailureIsUnexpected_Returns500WithAGenericMessage()
    {
        NewsController controller =
            CreateController(new FakeNewsApi { Failure = new InvalidOperationException("secret stack trace") });

        IActionResult result = await FilteredAction.InvokeAsync(controller, () => controller.Refresh(new NewsQueryDto()));

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(objectResult.Value));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
