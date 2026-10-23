using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.News;

public class NewsServiceSearchTests
{
    private const string BbcFeedUrl = "http://feeds.bbci.co.uk/news/business/rss.xml";

    private const string BbcFeed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel>
          <item><title>STOCK Market Rally</title><description>Shares climb</description><pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate></item>
          <item><title>Housing update</title><description>Mortgage RATES rise again</description><pubDate>Mon, 19 Oct 2026 09:00:00 GMT</pubDate></item>
          <item><title>Weather</title><description>Sunny skies</description><pubDate>Mon, 19 Oct 2026 08:00:00 GMT</pubDate></item>
        </channel></rss>
        """;

    private static NewsService CreateService()
    {
        var handler = new StubFeedHandler();
        return new NewsService(new HttpClient(handler), NullLogger<NewsService>.Instance);
    }

    [Theory]
    [InlineData("stock", "STOCK Market Rally")]
    [InlineData("Stock", "STOCK Market Rally")]
    [InlineData("rates", "Housing update")]
    [InlineData("MORTGAGE", "Housing update")]
    public async Task GetFinanceNewsAsync_SearchMatchesTitleOrDescriptionIgnoringCase(string search, string expectedTitle)
    {
        NewsService service = CreateService();

        List<NewsArticle> result = await service.GetFinanceNewsAsync(new NewsQueryOptions { Search = search });

        NewsArticle article = Assert.Single(result);
        Assert.Equal(expectedTitle, article.Title);
    }

    [Fact]
    public async Task GetFinanceNewsAsync_SearchMatchesSourceIgnoringCase()
    {
        NewsService service = CreateService();

        List<NewsArticle> result = await service.GetFinanceNewsAsync(new NewsQueryOptions { Search = "bbc BUSINESS" });

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetFinanceNewsAsync_SearchWithNoMatchReturnsEmpty()
    {
        NewsService service = CreateService();

        List<NewsArticle> result = await service.GetFinanceNewsAsync(new NewsQueryOptions { Search = "crypto" });

        Assert.Empty(result);
    }

    private sealed class StubFeedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = request.RequestUri?.ToString() == BbcFeedUrl
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BbcFeed, Encoding.UTF8, "application/rss+xml")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }
}
