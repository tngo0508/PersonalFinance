using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.ApiService.Features.Watchlist.Adapters;
using PersonalFinance.Shared.DTOs.News;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Watchlist;

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
            new ArticlePageClient(new HttpClient(handler), NullLogger<ArticlePageClient>.Instance),
            cache ?? new MemoryCache(new MemoryCacheOptions()));

    private const string ArticleUrl = "https://example.test/story";
    private const string GoogleArticleUrl = "https://news.google.com/rss/articles/CBMiabc?oc=5";

    /// <summary>A BBC feed with one item whose description and full content are given.</summary>
    private static string ItemFeed(string description, string? content = null, string url = ArticleUrl, string title = "Rates fall") =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/"><channel>
           <item><title>{title}</title><link>{url}</link><description>{description}</description>
           {(content is null ? "" : $"<content:encoded><![CDATA[{content}]]></content:encoded>")}
           <pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate></item>
         </channel></rss>
         """;

    private static string Page(string paragraphs) =>
        $"<html><head><title>Page</title><style>p {{ color: red }}</style></head><body><nav><p>Menu</p></nav>" +
        $"<script>var x = 'script noise that must not be summarized';</script><article>{paragraphs}</article></body></html>";

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
    public async Task GetNewsAsync_FilesEveryArticleUnderItsSourcesTopic_IgnoringTheFeedsOwnCategories()
    {
        // The BBC stub's "Housing update" item carries <category>Debt</category>; BBC Business is filed under General.
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.All(feed.Articles, a => Assert.Equal("General", a.Category));
    }

    [Fact]
    public async Task GetNewsAsync_FiltersByTopicIgnoringCase()
    {
        NewsService service = CreateService(new StubFeedHandler());

        Assert.Equal(3, (await service.GetNewsAsync(new NewsQueryDto { Category = "general" })).Articles.Count);
        Assert.Empty((await service.GetNewsAsync(new NewsQueryDto { Category = "debt" })).Articles);
    }

    [Fact]
    public async Task GetNewsAsync_ReadsTheConfiguredSources_EachUnderItsTopic()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(
            [
                ("NPR Personal Finance", "Budgeting"), ("NerdWallet", "Budgeting"), ("Google News: Personal finance", "Budgeting"),
                ("MarketWatch", "Investing"), ("CNBC Investing", "Investing"), ("Yahoo Finance Markets", "Investing"),
                ("Retirement Researcher", "Retirement"), ("Google News: Retirement", "Retirement"),
                ("BBC Business", "General"),
            ],
            feed.Sources.Select(s => (s.Name, s.Category)));
        Assert.All(feed.Sources, s => Assert.StartsWith("http", s.Url));
    }

    [Fact]
    public async Task GetNewsAsync_OffersAsTopics_ExactlyTheCategoriesOfTheConfiguredSources_InSourceOrder()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(["Budgeting", "Investing", "Retirement", "General"], feed.Topics);
        Assert.Equal(feed.Sources.Select(s => s.Category).Distinct(), feed.Topics);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsArticlesNewestFirst_WithHtmlStripped_AndTheFilterOptions()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(["STOCK Market Rally", "Housing update", "Weather"], feed.Articles.Select(a => a.Title));
        Assert.Equal("Shares climb", feed.Articles[0].Description);
        Assert.Equal("BBC Business", feed.Articles[0].Source);
        Assert.Contains(feed.Sources, s => s.Name == "BBC Business");
    }

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsTheOptionsOfTheWholeFeed_WithoutArticles()
    {
        NewsFeedDto options = await CreateService(new StubFeedHandler()).GetFilterOptionsAsync();

        Assert.Empty(options.Articles);
        Assert.Empty(options.Errors);
        Assert.Contains(options.Sources, s => s.Name == "BBC Business");
        Assert.Equal(["Budgeting", "Investing", "Retirement", "General"], options.Topics);
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

    [Theory]
    [InlineData("Mon, 19 Oct 2026 10:00:00 GMT", "2026-10-19T10:00:00Z")]
    [InlineData("Tue, 20 Oct 2026 08:00:00 -0400", "2026-10-20T12:00:00Z")]
    [InlineData("Tue, 20 Oct 2026 08:00:00 +0530", "2026-10-20T02:30:00Z")]
    [InlineData("Tue, 20 Oct 2026 08:00:00 EST", "2026-10-20T13:00:00Z")]
    [InlineData("Tue, 20 Oct 2026 08:00:00 PDT", "2026-10-20T15:00:00Z")]
    [InlineData("20 Oct 2026 08:00 UT", "2026-10-20T08:00:00Z")]
    public async Task GetNewsAsync_ReadsRfc822PubDatesAsUtc_WhateverTheServerCulture(string pubDate, string expectedUtc)
    {
        var handler = new StubFeedHandler
        {
            Feed = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <rss version="2.0"><channel><item><title>Dated</title><pubDate>{pubDate}</pubDate></item></channel></rss>
                """
        };
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            NewsFeedDto feed = await CreateService(handler).GetNewsAsync(new NewsQueryDto());

            DateTime publishedAt = Assert.Single(feed.Articles).PublishedAt;
            Assert.Equal(DateTimeKind.Utc, publishedAt.Kind);
            Assert.Equal(DateTime.Parse(expectedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal), publishedAt);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task GetNewsAsync_KeepsTheNewestFiveArticlesOfASource_WhateverTheFeedOrder_AndTruncatesLongDescriptions()
    {
        // Item 1 is the newest; the feed lists them oldest first, as a relevance-ordered search feed might.
        string items = string.Concat(Enumerable.Range(1, 7).Reverse().Select(i =>
            $"<item><title>Item {i}</title><description>{new string('x', 300)}</description>" +
            $"<pubDate>Mon, 19 Oct 2026 {10 - i:00}:00:00 GMT</pubDate></item>"));
        var handler = new StubFeedHandler
        {
            Feed = $"""<?xml version="1.0" encoding="UTF-8"?><rss version="2.0"><channel>{items}</channel></rss>"""
        };

        NewsFeedDto feed = await CreateService(handler).GetNewsAsync(new NewsQueryDto());

        Assert.Equal(["Item 1", "Item 2", "Item 3", "Item 4", "Item 5"], feed.Articles.Select(a => a.Title));
        Assert.All(feed.Articles, a => Assert.Equal(new string('x', 250) + "...", a.Description));
    }

    [Fact]
    public async Task GetNewsAsync_ReadsAGoogleNewsItem_WithThePublisherFromItsSource_AndNoDescriptionRepeatingTheTitle()
    {
        var handler = new StubFeedHandler();
        handler.Responses["news.google.com/rss/search"] = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
              <item><title>Credit card debt nears a record - ABC News</title><link>{GoogleArticleUrl}</link>
                <description>&lt;a href="{GoogleArticleUrl}"&gt;Credit card debt nears a record&lt;/a&gt;&amp;nbsp;&amp;nbsp;&lt;font color="#6f6f6f"&gt;ABC News&lt;/font&gt;</description>
                <source url="https://abcnews.go.com">ABC News</source><pubDate>Mon, 19 Oct 2026 11:00:00 GMT</pubDate></item>
            </channel></rss>
            """;

        NewsFeedDto feed = await CreateService(handler).GetNewsAsync(new NewsQueryDto { Source = "Google News: Personal finance" });

        NewsArticleDto article = Assert.Single(feed.Articles);
        Assert.Equal("Credit card debt nears a record", article.Title);
        Assert.Equal("ABC News", article.Publisher);
        Assert.Equal("", article.Description);
        Assert.Equal("Budgeting", article.Category);
    }

    [Fact]
    public async Task GetNewsAsync_SkipsAnAggregatorItem_WhoseHeadlineIsOnlyThePublisher()
    {
        // Google News sometimes ships an item with no headline: its title is just " - Publisher".
        var handler = new StubFeedHandler();
        handler.Responses["news.google.com/rss/search"] = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
              <item><title> - money.com</title><link>{GoogleArticleUrl}</link><description>money.com</description>
                <source url="https://money.com">money.com</source><pubDate>Mon, 19 Oct 2026 11:00:00 GMT</pubDate></item>
              <item><title>Rates fall - money.com</title><link>{GoogleArticleUrl}2</link><description>Rates fall</description>
                <source url="https://money.com">money.com</source><pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate></item>
            </channel></rss>
            """;

        NewsFeedDto feed = await CreateService(handler).GetNewsAsync(new NewsQueryDto { Source = "Google News: Personal finance" });

        Assert.Equal("Rates fall", Assert.Single(feed.Articles).Title);
    }

    [Fact]
    public async Task GetNewsAsync_LeavesThePublisherEmpty_ForAFeedWithoutSourceElements()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler()).GetNewsAsync(new NewsQueryDto());

        Assert.All(feed.Articles, a => Assert.Null(a.Publisher));
    }

    [Fact]
    public async Task GetArticleTextAsync_ReturnsTheFeedsFullContent_AsTheFullArticle_WithoutReadingThePage()
    {
        string body = string.Concat(Enumerable.Range(1, 12).Select(i => $"<p>Paragraph {i} of the article body, long enough to count as real text.</p>"));
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell <b>today</b>", body) };

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(ArticleUrl);

        Assert.NotNull(text);
        Assert.Equal("Rates fall", text.Title);
        Assert.Equal(NewsSummaryCoverage.FullArticle, text.Coverage);
        Assert.StartsWith("Paragraph 1 of the article body", text.Text);
        Assert.Contains("Paragraph 12", text.Text);
        Assert.DoesNotContain("<p>", text.Text);
        Assert.DoesNotContain(ArticleUrl, handler.RequestedUrls);
    }

    [Fact]
    public async Task GetArticleTextAsync_ReadsTheArticlePage_WhenTheFeedCarriesOnlyAnExcerpt()
    {
        string paragraphs = string.Concat(Enumerable.Range(1, 12).Select(i => $"<p>Page paragraph {i}: the full story as published, with enough words to matter.</p>"));
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell today.") };
        handler.Responses[ArticleUrl] = Page(paragraphs);

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(ArticleUrl);

        Assert.NotNull(text);
        Assert.Equal(NewsSummaryCoverage.FullArticle, text.Coverage);
        Assert.StartsWith("Page paragraph 1:", text.Text);
        Assert.Contains("Page paragraph 12", text.Text);
        Assert.DoesNotContain("script noise", text.Text);
        Assert.DoesNotContain("Menu", text.Text);
        Assert.DoesNotContain("color: red", text.Text);
    }

    [Fact]
    public async Task GetArticleTextAsync_FallsBackToTheFeedExcerpt_WhenThePageCannotBeRead()
    {
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell <b>today</b>, the central bank said.") };
        handler.Responses[ArticleUrl] = new HttpResponseMessage(HttpStatusCode.Forbidden);

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(ArticleUrl);

        Assert.NotNull(text);
        Assert.Equal(NewsSummaryCoverage.FeedExcerpt, text.Coverage);
        Assert.Equal("Rates fell today, the central bank said.", text.Text);
    }

    [Fact]
    public async Task GetArticleTextAsync_FallsBackToTheFeedExcerpt_WhenThePageHasTooLittleText()
    {
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell today, the central bank said.") };
        handler.Responses[ArticleUrl] = Page("<p>Subscribe to continue reading this article.</p>");

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(ArticleUrl);

        Assert.Equal(NewsSummaryCoverage.FeedExcerpt, text?.Coverage);
        Assert.Equal("Rates fell today, the central bank said.", text?.Text);
    }

    [Fact]
    public async Task GetArticleTextAsync_NeverReadsAGoogleNewsLink_AndOffersTheHeadlineOnly()
    {
        var handler = new StubFeedHandler();
        handler.Responses["news.google.com/rss/search"] = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><item><title>Pensions change - Reuters</title><link>{GoogleArticleUrl}</link>
              <description>&lt;a href="{GoogleArticleUrl}"&gt;Pensions change&lt;/a&gt; Reuters</description>
              <source url="https://www.reuters.com">Reuters</source><pubDate>Mon, 19 Oct 2026 11:00:00 GMT</pubDate></item></channel></rss>
            """;

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(GoogleArticleUrl);

        Assert.NotNull(text);
        Assert.Equal(NewsSummaryCoverage.HeadlineOnly, text.Coverage);
        Assert.Equal("Pensions change", text.Title);
        Assert.Equal("Pensions change", text.Text);
        Assert.DoesNotContain(GoogleArticleUrl, handler.RequestedUrls);
    }

    [Fact]
    public async Task GetArticleTextAsync_ReturnsNull_ForAnArticleNotInTheFeed()
    {
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell today.") };

        Assert.Null(await CreateService(handler).GetArticleTextAsync("https://evil.test/anything"));
        Assert.DoesNotContain("https://evil.test/anything", handler.RequestedUrls);
    }

    [Fact]
    public async Task GetArticleTextAsync_CapsTheTextAtSixThousandCharacters()
    {
        string body = string.Concat(Enumerable.Range(1, 200).Select(i => $"<p>Paragraph {i} with enough words to pass the short-line filter easily.</p>"));
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell today.", body) };

        NewsArticleText? text = await CreateService(handler).GetArticleTextAsync(ArticleUrl);

        Assert.Equal(6000, text?.Text.Length);
    }

    [Fact]
    public async Task GetArticleTextAsync_ReadsThePageOnce_AcrossCallsSharingTheCache()
    {
        string paragraphs = string.Concat(Enumerable.Range(1, 12).Select(i => $"<p>Page paragraph {i}: the full story as published, with enough words to matter.</p>"));
        var handler = new StubFeedHandler { Feed = ItemFeed("Rates fell today.") };
        handler.Responses[ArticleUrl] = Page(paragraphs);
        var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateService(handler, cache).GetArticleTextAsync(ArticleUrl);
        await CreateService(handler, cache).GetArticleTextAsync(ArticleUrl);

        Assert.Equal(1, handler.RequestedUrls.Count(u => u == ArticleUrl));
    }

    [Fact]
    public async Task GetNewsAsync_SkipsASourceWhoseFeedIsNotXml()
    {
        NewsFeedDto feed = await CreateService(new StubFeedHandler { Feed = "not xml at all" }).GetNewsAsync(new NewsQueryDto());

        Assert.Empty(feed.Articles);
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

        /// <summary>The BBC feed; every other source answers 404 unless <see cref="Responses"/> names it.</summary>
        public string Feed { get; init; } = BbcFeed;

        /// <summary>
        /// Other responses by URL, or by a substring of the URL: a string is served as a 200 (RSS when it is XML, HTML
        /// otherwise), an <see cref="HttpResponseMessage"/> as given.
        /// </summary>
        public Dictionary<string, object> Responses { get; } = new(StringComparer.Ordinal);

        public List<string> RequestedUrls { get; } = [];
        public int Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            string url = request.RequestUri?.ToString() ?? string.Empty;
            lock (RequestedUrls)
            {
                RequestedUrls.Add(url);
            }

            if (Throw)
            {
                throw new HttpRequestException("feed down");
            }

            if (url == BbcFeedUrl)
            {
                return Task.FromResult(Rss(Feed));
            }

            object? match = Responses.TryGetValue(url, out object? exact)
                ? exact
                : Responses.FirstOrDefault(r => url.Contains(r.Key, StringComparison.Ordinal)).Value;
            return Task.FromResult(match switch
            {
                HttpResponseMessage response => response,
                string body when body.TrimStart().StartsWith("<?xml", StringComparison.Ordinal) => Rss(body),
                string body => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/html")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }

        private static HttpResponseMessage Rss(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/rss+xml")
        };
    }
}
