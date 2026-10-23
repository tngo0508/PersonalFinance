using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.ApiService.Features.Watchlist.Adapters;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.Tests.News;

/// <summary>
/// The ApiService news feed of the Watchlist Feature: RSS articles are filtered by category, source and free text,
/// and the fetched feed is cached across requests.
/// </summary>
public class NewsServiceTests
{
    private const string BbcFeedUrl = "http://feeds.bbci.co.uk/news/business/rss.xml";

    private const string BbcFeed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel>
          <item><title>STOCK Market Rally</title><description>Shares &lt;b&gt;climb&lt;/b&gt;</description><pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate></item>
          <item><title>Housing update</title><description>Mortgage RATES rise again</description><category>Debt</category><pubDate>Mon, 19 Oct 2026 09:00:00 GMT</pubDate></item>
          <item><title>Weather</title><description>Sunny skies</description><pubDate>Mon, 19 Oct 2026 08:00:00 GMT</pubDate></item>
        </channel></rss>
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static NewsService CreateService(StubFeedHandler handler, IMemoryCache? cache = null) =>
        new(new RssNewsFeedClient(new HttpClient(handler), new FixedTimeProvider(Now),
                NullLogger<RssNewsFeedClient>.Instance),
            cache ?? new MemoryCache(new MemoryCacheOptions()));

    [Theory]
    [InlineData("stock", "STOCK Market Rally")]
    [InlineData("Stock", "STOCK Market Rally")]
    [InlineData("rates", "Housing update")]
    [InlineData("MORTGAGE", "Housing update")]
    public async Task GetNewsAsync_SearchMatchesTitleOrDescriptionIgnoringCase(string search, string expectedTitle)
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto { Search = search });

        Assert.Equal(expectedTitle, Assert.Single(feed.Articles).Title);
    }

    [Fact]
    public async Task GetNewsAsync_SearchMatchesSourceIgnoringCase()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto { Search = "bbc BUSINESS" });

        Assert.Equal(3, feed.Articles.Count);
    }

    [Fact]
    public async Task GetNewsAsync_SearchWithNoMatchReturnsEmpty()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto { Search = "crypto" });

        Assert.Empty(feed.Articles);
    }

    [Fact]
    public async Task GetNewsAsync_FiltersByCategory_ButListsTheCategoriesOfTheWholeFeed()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto { Category = "debt" });

        Assert.Equal("Housing update", Assert.Single(feed.Articles).Title);
        Assert.Equal(["Debt", "General"], feed.Categories);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsArticlesNewestFirst_WithHtmlStripped_AndTheFilterOptions()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(["STOCK Market Rally", "Housing update", "Weather"], feed.Articles.Select(a => a.Title));
        Assert.Equal("Shares climb", feed.Articles[0].Description);
        Assert.Equal("BBC Business", feed.Articles[0].Source);
        Assert.Contains(feed.Sources, s => s.Name == "BBC Business");
        Assert.Equal(["Budgeting", "Saving", "Investing", "Debt", "Retirement"], feed.PersonalFinanceCategories);
    }

    [Fact]
    public async Task GetNewsAsync_ReadsTheFeedsOnce_AcrossRequestsSharingTheCache()
    {
        var handler = new StubFeedHandler();
        var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(handler, cache).GetNewsAsync(new NewsQueryDto());
        int requestsAfterFirstCall = handler.Requests;
        await CreateService(handler, cache).GetNewsAsync(new NewsQueryDto { Search = "stock" });

        Assert.True(requestsAfterFirstCall > 0);
        Assert.Equal(requestsAfterFirstCall, handler.Requests);
    }

    [Fact]
    public async Task GetNewsAsync_DatesAnArticleWithoutAReadablePubDate_AtTheCurrentTime()
    {
        var handler = new StubFeedHandler
        {
            Feed = """
                <?xml version="1.0" encoding="UTF-8"?>
                <rss version="2.0"><channel><item><title>Undated</title><pubDate>not a date</pubDate></item></channel></rss>
                """
        };

        NewsFeedDto feed = await CreateService(handler).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(Now.UtcDateTime, Assert.Single(feed.Articles).PublishedAt);
    }

    [Fact]
    public async Task GetNewsAsync_SkipsAFailingSource()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler { Throw = true }).GetNewsAsync(new NewsQueryDto());

        Assert.Empty(feed.Articles);
        Assert.NotEmpty(feed.Sources);
    }

    private sealed class StubFeedHandler : HttpMessageHandler
    {
        private int _requests;

        public bool Throw { get; init; }
        public string Feed { get; init; } = BbcFeed;
        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            if (Throw)
            {
                throw new HttpRequestException("feed down");
            }

            HttpResponseMessage response = request.RequestUri?.ToString() == BbcFeedUrl
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Feed, Encoding.UTF8, "application/rss+xml")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
