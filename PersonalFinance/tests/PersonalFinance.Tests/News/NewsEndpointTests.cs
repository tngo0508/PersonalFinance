using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.ApiService.Features.Watchlist.Validators;
using PersonalFinance.Shared.DTOs.Watchlist;
using NewsApiController = PersonalFinance.ApiService.Features.Watchlist.NewsController;

namespace PersonalFinance.Tests.News;

/// <summary>
/// The ApiService <c>GET /api/News</c> endpoint: signed-in only, and its filters are validated before the feed is read.
/// </summary>
public class NewsEndpointTests
{
    private sealed class FakeNewsService : INewsService
    {
        public NewsFeedDto Feed { get; } = new() { Articles = [new NewsArticleDto { Title = "Rates fall" }] };
        public NewsQueryDto? LastQuery { get; private set; }

        public Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Feed);
        }
    }

    [Fact]
    public void Endpoint_RequiresAuthentication()
    {
        Assert.NotNull(typeof(NewsApiController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Get_ValidQuery_ReturnsTheFeed()
    {
        var service = new FakeNewsService();
        var query = new NewsQueryDto { Category = "Investing", Search = "rates" };

        IActionResult result = await new NewsApiController(service, new NewsQueryDtoValidator()).Get(query);

        Assert.Same(service.Feed, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Same(query, service.LastQuery);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("source")]
    [InlineData("search")]
    public async Task Get_OverlongFilter_ReturnsBadRequest_WithoutReadingTheFeed(string filter)
    {
        var service = new FakeNewsService();
        string overlong = new('x', NewsQueryDtoValidator.MaxFilterLength + 1);
        var query = new NewsQueryDto
        {
            Category = filter == "category" ? overlong : null,
            Source = filter == "source" ? overlong : null,
            Search = filter == "search" ? overlong : null,
        };

        IActionResult result = await new NewsApiController(service, new NewsQueryDtoValidator()).Get(query);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(service.LastQuery);
    }
}
