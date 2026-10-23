using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.News;
using Refit;

namespace PersonalFinance.Tests.News;

/// <summary>
/// The Web Finance News page reads the feed from ApiService through <see cref="INewsApi"/>. Filters ApiService
/// rejects find no articles; a failure shows a message and never carries exception details.
/// </summary>
public class NewsWebControllerTests
{
    private sealed class FakeNewsApi : INewsApi
    {
        public Exception? Failure { get; init; }

        /// <summary>Answers any filtered query with 400, as ApiService does for an overlong filter.</summary>
        public bool RejectFilters { get; init; }

        public List<NewsQueryDto> Queries { get; } = [];

        public NewsFeedDto Feed { get; } = new()
        {
            Articles = [new NewsArticleDto { Title = "Rates fall", Url = "https://example.test/rates", Category = "Investing" }],
            Categories = ["General", "Investing"],
            Sources = [new NewsSourceDto { Name = "BBC Business" }],
            PersonalFinanceCategories = ["Budgeting", "Saving", "Investing", "Debt", "Retirement"],
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
                throw await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/News"),
                    HttpMethod.Get, new HttpResponseMessage(HttpStatusCode.BadRequest), new RefitSettings());
            }

            return Feed;
        }
    }

    private static NewsController CreateController(INewsApi api) => new(api, NullLogger<NewsController>.Instance)
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
        Assert.Equal(["General", "Investing"], model.Categories);
        Assert.Equal("BBC Business", Assert.Single(model.Sources).Name);
        Assert.Equal(5, model.PersonalFinanceCategories.Count);
        Assert.Equal("Investing", model.SelectedCategory);
        Assert.Equal("BBC Business", model.SelectedSource);
        Assert.Equal("rates", model.SearchQuery);
        Assert.False(model.IsLoading);
        Assert.NotNull(model.LastRefreshed);
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
        Assert.Equal("too long", model.SearchQuery);
        Assert.False(model.IsLoading);
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
    }

    [Fact]
    public async Task Refresh_WhenApiServiceRejectsTheFilters_ReturnsNoArticles()
    {
        var api = new FakeNewsApi { RejectFilters = true };

        NewsRefreshResponse response =
            RefreshResponseOf(await CreateController(api).Refresh(new NewsQueryDto { Search = "too long" }));

        Assert.True(response.Success);
        Assert.Empty(response.Articles!);
    }

    [Fact]
    public async Task Refresh_WhenApiServiceFails_ReturnsAnErrorWithoutExceptionDetails()
    {
        var api = new FakeNewsApi { Failure = new InvalidOperationException("secret stack trace") };

        NewsRefreshResponse response = RefreshResponseOf(await CreateController(api).Refresh(new NewsQueryDto()));

        Assert.False(response.Success);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(response));
    }
}
