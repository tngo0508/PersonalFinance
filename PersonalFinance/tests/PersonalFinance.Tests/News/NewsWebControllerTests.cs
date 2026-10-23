using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Models.News;

namespace PersonalFinance.Tests.News;

/// <summary>
/// The Web Finance News page reads the feed from ApiService through <see cref="INewsApi"/>; a failure shows a message
/// and never carries exception details.
/// </summary>
public class NewsWebControllerTests
{
    private sealed class FakeNewsApi : INewsApi
    {
        public Exception? Failure { get; init; }
        public NewsQueryDto? LastQuery { get; private set; }

        public NewsFeedDto Feed { get; } = new()
        {
            Articles = [new NewsArticleDto { Title = "Rates fall", Url = "https://example.test/rates", Category = "Investing" }],
            Categories = ["General", "Investing"],
            Sources = [new NewsSourceDto { Name = "BBC Business" }],
            PersonalFinanceCategories = ["Budgeting", "Saving", "Investing", "Debt", "Retirement"],
        };

        public Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Failure is null ? Task.FromResult(Feed) : Task.FromException<NewsFeedDto>(Failure);
        }
    }

    private static NewsController CreateController(INewsApi api) => new(api, NullLogger<NewsController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task Index_BuildsTheViewModelFromTheFeed_AndForwardsTheFilters()
    {
        var api = new FakeNewsApi();

        IActionResult result = await CreateController(api).Index("Investing", "BBC Business", "rates");

        NewsViewModel model = Assert.IsType<NewsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Rates fall", Assert.Single(model.Articles).Title);
        Assert.Equal(["General", "Investing"], model.Categories);
        Assert.Equal("BBC Business", Assert.Single(model.Sources).Name);
        Assert.Equal(5, model.PersonalFinanceCategories.Count);
        Assert.Equal("Investing", model.SelectedCategory);
        Assert.Equal("rates", model.SearchQuery);
        Assert.False(model.IsLoading);
        Assert.NotNull(model.LastRefreshed);
        Assert.Null(model.ErrorMessage);
        Assert.Equal("Investing", api.LastQuery!.Category);
        Assert.Equal("BBC Business", api.LastQuery.Source);
        Assert.Equal("rates", api.LastQuery.Search);
    }

    [Fact]
    public async Task Index_WhenApiServiceFails_ShowsAMessageWithoutExceptionDetails()
    {
        var api = new FakeNewsApi { Failure = new InvalidOperationException("secret stack trace") };

        IActionResult result = await CreateController(api).Index(null, null, null);

        NewsViewModel model = Assert.IsType<NewsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Articles);
        Assert.False(model.IsLoading);
        Assert.NotNull(model.ErrorMessage);
        Assert.DoesNotContain("secret", model.ErrorMessage);
    }

    [Fact]
    public async Task Refresh_ReturnsTheArticlesAsJson()
    {
        IActionResult result = await CreateController(new FakeNewsApi()).Refresh(null, null, "rates");

        NewsRefreshResponse response = Assert.IsType<NewsRefreshResponse>(Assert.IsType<JsonResult>(result).Value);
        Assert.True(response.Success);
        Assert.Equal("Rates fall", Assert.Single(response.Articles!).Title);
    }

    [Fact]
    public async Task Refresh_WhenApiServiceFails_ReturnsAnErrorWithoutExceptionDetails()
    {
        var api = new FakeNewsApi { Failure = new InvalidOperationException("secret stack trace") };

        IActionResult result = await CreateController(api).Refresh(null, null, null);

        NewsRefreshResponse response = Assert.IsType<NewsRefreshResponse>(Assert.IsType<JsonResult>(result).Value);
        Assert.False(response.Success);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(response));
    }
}
