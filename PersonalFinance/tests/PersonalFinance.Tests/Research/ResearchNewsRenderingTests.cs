using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research and Finance News pages (slice L3, #359) rendered through the real compiled Razor views: their chrome
/// shows no English when the UI language is Vietnamese, dates follow the request culture, and each page carries its JS
/// strings in its own island. Third-party data (company, fund and country names, headlines) stays as sourced.
/// </summary>
public sealed partial class ResearchNewsRenderingTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";
    private const string SignedInHeader = "X-Test-Signed-In";

    // Visible English chrome of the Research page with every dataset loaded. Each must render in English, and none
    // may render in Vietnamese.
    // Each Research page's own English, checked on that page; the shared nav and disclaimer are checked on Home.
    public static TheoryData<string, string[]> ResearchPages => new()
    {
        {
            "/Research",
            [
                ">Sectors<", ">World<", ">Your basket<", "Explore the market", "Data freshness", "New here?",
                "Start from a mix:", "Open basket report", "For information only. Not investment advice.", "Try:",
            ]
        },
        {
            "/Research/Sectors",
            [
                "Largest SEC filers by Public float", "largest of", "Show as table", "Key takeaways", "View options",
                "SEC industry offices", "About this data", "Data as of", "Play through the years", "31 December 2025",
            ]
        },
        {
            "/Research/World",
            ["Stock-market value by country", "Colour the map by", "Market cap", "World market", "Your basket by issuer country"]
        },
        {
            "/Research/Company",
            [
                "Company lookup", "Look up a company", "Ticker or company name", "e.g. MSFT or Microsoft", ">Look up<",
                "Press releases", "Recent filings", "All filings on EDGAR", "(opens in a new tab)", "Source:",
            ]
        },
        {
            "/Research/Basket?basket=VTI",
            ["Fund Basket", "Use equal weights", "Your basket at a glance", "Compare your basket with", "Adjusted for inflation"]
        },
        {
            "/Research/Fund/VTI",
            ["Add to basket", "Compare all funds", "What it holds", "Where the money goes", "Similar funds", "Growth of 100"]
        },
    };

    // Each page's Unavailable State when no dataset has loaded.
    public static TheoryData<string, string[]> ResearchUnavailablePages => new()
    {
        { "/Research", ["Not loaded yet"] },
        { "/Research/Sectors", ["We couldn't load the Sectors data"] },
        { "/Research/World", ["We couldn't load the World data"] },
        { "/Research/Basket?basket=VTI", ["We couldn't load the funds data"] },
    };

    private static readonly string[] NewsEnglish =
    [
        "Budgeting, investing and retirement stories from trusted sources, newest first.",
        "Search headlines and sources", "Search news articles", "Filter by topic", "Filter by source", "All Sources",
        "Last updated:", "Refresh news", "AI-generated summary", "Save article to watchlist", "Summarize with AI",
        "My Watchlist", "Your watchlist and news sources", ">Sources<", ">Older<", "(opens in a new tab)",
        "Loading your saved articles", " article<"
    ];

    private static readonly string[] NewsEmptyEnglish =
        ["No articles found", "Try adjusting your search or filter criteria.", "View All News"];

    private static readonly SectorsSnapshotDto Sectors = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Periods = ["2024", "2025"],
        Companies = [new SectorsCompanyDto { Cik = 320193, Name = "APPLE INC" }],
        Values =
        [
            new SectorsPeriodDto { Period = "2024", Ranked = [new SectorsRankedValueDto { Company = 0, PublicFloat = 2m }] },
            new SectorsPeriodDto
            {
                Period = "2025",
                Ranked = [new SectorsRankedValueDto { Company = 0, PublicFloat = 2_600m }],
                RestTotal = 40m,
                RestCount = 1_234,
            },
        ],
    };

    private static readonly FundsSnapshotDto Funds = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Periods = ["2025 Q4"],
        Funds = [new TrackedFundDto { Ticker = "VTI", Name = "Vanguard Total Stock Market ETF" }],
        Values =
        [
            new FundsPeriodDto
            {
                Period = "2025 Q4",
                Funds = [new FundHoldingsDto { Ticker = "VTI", ReportDate = new DateOnly(2025, 12, 31), NetAssets = 1m }],
            },
        ],
    };

    private static readonly FundReturnsSnapshotDto Returns = new() { AsOf = new DateOnly(2025, 12, 1) };

    private static readonly MacroSnapshotDto Macro = new()
    {
        AsOf = new DateOnly(2025, 12, 1),
        Months = [new MacroMonthDto { Month = new DateOnly(2025, 12, 1), ConsumerPrices = 320m }],
    };

    private static readonly WorldSnapshotDto World = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Periods = ["2024"],
        Countries = [new WorldCountryDto { Id = "840", Iso3 = "USA", Name = "United States" }],
        Values = [new WorldPeriodDto { Period = "2024", Countries = [new WorldCountryValueDto { Country = 0, MarketCap = 5e13m }] }],
    };

    private static readonly FundOverlapSnapshotDto Overlap = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Funds = [new FundOverlapFundDto { Ticker = "VTI", ReportDate = new DateOnly(2025, 12, 31) }],
    };

    private static readonly FundExposureSnapshotDto Exposure = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Funds = [new FundExposureDto { Ticker = "VTI", ReportDate = new DateOnly(2025, 12, 31) }],
    };

    private static readonly NewsFeedDto Feed = new()
    {
        Topics = ["Investing"],
        Sources = [new NewsSourceDto { Name = "Example Wire" }],
        Articles =
        [
            new NewsArticleDto
            {
                Title = "Markets close higher", Url = "https://news.example/1", Source = "Example Wire",
                Category = "Investing", PublishedAt = new DateTime(2025, 12, 30, 14, 5, 0, DateTimeKind.Utc),
            },
        ],
    };

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private ApiProxy<IResearchApi> _research = null!;
    private ApiProxy<INewsApi> _news = null!;

    public async Task InitializeAsync()
    {
        (IResearchApi researchApi, ApiProxy<IResearchApi> research) = ApiProxy.Create<IResearchApi>();
        (INewsApi newsApi, ApiProxy<INewsApi> news) = ApiProxy.Create<INewsApi>();
        _research = research;
        _news = news;

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = RepositoryRoot.SourceProject("PersonalFinance.Web"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(researchApi);
        builder.Services.AddSingleton(TestResearchCache.Create());
        builder.Services.AddSingleton(newsApi);
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddIdentityCore<IdentityUser>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, HeaderSignInHandler>(IdentityConstants.ApplicationScheme, null);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddSingleton<PersonalFinance.Web.Services.ILearningLibraryStatus>(new PersonalFinance.Tests.TestSupport.FixedLearningLibraryStatus(false));
        builder.Services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddApplicationPart(typeof(ResearchController).Assembly);

        _app = builder.Build();
        _app.UseRequestLocalization();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAntiforgery();
        _app.MapControllerRoute("default", "{controller}/{action=Index}");
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(ResearchPages))]
    public async Task ResearchPage_InVietnamese_ShowsNoEnglishChrome(string url, string[] english)
    {
        LoadEveryDataset();

        string inEnglish = await GetAsync(url, vietnamese: false);
        string html = await GetAsync(url, vietnamese: true);

        AssertShows(inEnglish, english);
        AssertHides(html, english);
        Assert.Contains(TestLocalization.Vietnamese("Research.Title"), html);
    }

    [Fact]
    public async Task SectorsPage_WhenOnlyTheFundsDataFails_SaysTheBasketViewIsUnavailable()
    {
        _research.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors);

        string html = await GetAsync("/Research/Sectors", vietnamese: false);

        Assert.Contains("id=\"research-sectors-basket-unavailable\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-sectors-view=\"basket\"", html, StringComparison.Ordinal);
        Assert.Contains("research-treemap", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Research/Funds", "Compare the tracked funds")]
    [InlineData("/Research/Funds?view=risk", "research-risk-chart")]
    [InlineData("/Research/Funds?view=correlation", "data-correlation-all=\"true\"")]
    [InlineData("/Research/Funds?view=holdings", "research-holdings-list")]
    public async Task FundsPage_RendersEachView(string url, string expected)
    {
        LoadEveryDataset();

        string html = await GetAsync(url, vietnamese: false);

        Assert.Contains(expected, html, StringComparison.Ordinal);
        // The fee of a fund without filed facts reads "not filed" in the table.
        if (url == "/Research/Funds")
        {
            Assert.Contains(">not filed<", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task FundsPage_WithoutReturns_ShowsTheTable_AndOnlyTheRiskViewIsUnavailable()
    {
        // The fund returns are not registered, so reading them fails as if ApiService were unreachable.
        _research.Returns(nameof(IResearchApi.GetFundsAsync), Funds)
            .Returns(nameof(IResearchApi.GetFundFactsAsync), new FundFactsSnapshotDto());

        string table = await GetAsync("/Research/Funds", vietnamese: false);
        string risk = await GetAsync("/Research/Funds?view=risk", vietnamese: false);

        Assert.Contains("id=\"research-compare-table\"", table, StringComparison.Ordinal);
        Assert.Contains("research-compare-no-returns", table, StringComparison.Ordinal);
        Assert.DoesNotContain("We couldn't load the fund returns data", table, StringComparison.Ordinal);
        Assert.Contains("We couldn't load the fund returns data", risk, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"research-risk-chart\"", risk, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FundsPage_HoldingsList_LinksEachHoldingByName_AndCarriesNoIdentifier()
    {
        LoadEveryDataset();
        _research.Returns(nameof(IResearchApi.GetFundsAsync), new FundsSnapshotDto
        {
            AsOf = new DateOnly(2025, 12, 31),
            Periods = ["2025 Q4"],
            Funds = [new TrackedFundDto { Ticker = "VTI", Name = "Vanguard Total Stock Market ETF" }],
            Values =
            [
                new FundsPeriodDto
                {
                    Period = "2025 Q4",
                    Funds =
                    [
                        new FundHoldingsDto
                        {
                            Ticker = "VTI", ReportDate = new DateOnly(2025, 12, 31), NetAssets = 1m, HoldingCount = 3,
                            Categories =
                            [
                                new FundCategoryDto
                                {
                                    Code = "EC", Name = "Equity, common", Percent = 99m, HoldingCount = 3,
                                    Top = [new FundHoldingDto { Name = "NVIDIA CORP", Percent = 7m }],
                                    OtherCount = 2, OtherPercent = 92m,
                                },
                            ],
                        },
                    ],
                },
            ],
        });

        string html = await GetAsync("/Research/Funds?view=holdings", vietnamese: false);

        Assert.Contains("href=\"/Research/Company?q=NVIDIA%20CORP\"", html, StringComparison.Ordinal);
        Assert.Contains("Other (2)", html, StringComparison.Ordinal);
        string list = html[html.IndexOf("research-holdings-list", StringComparison.Ordinal)..];
        Assert.DoesNotMatch(new Regex(@"\b[0-9A-Z]{8}[0-9]\b"), list);
        Assert.DoesNotMatch(new Regex(@"\b[A-Z]{2}[0-9A-Z]{9}[0-9]\b"), list);
    }

    [Fact]
    public async Task ResearchPage_InVietnamese_FormatsCountsAndDatesInTheRequestCulture()
    {
        LoadEveryDataset();

        string html = await GetAsync("/Research/Sectors", vietnamese: true);

        Assert.Contains("1.235", html);
        Assert.Contains("31 tháng 12 2025", html);
    }

    [Theory]
    [MemberData(nameof(ResearchUnavailablePages))]
    public async Task ResearchPage_WithNoData_InVietnamese_ShowsNoEnglishUnavailableState(string url, string[] english)
    {
        string inEnglish = await GetAsync(url, vietnamese: false);
        string html = await GetAsync(url, vietnamese: true);

        AssertShows(inEnglish, english);
        AssertHides(html, english);
    }

    [Fact]
    public async Task ResearchPage_CarriesTheResearchJsStrings_InTheirOwnIsland()
    {
        LoadEveryDataset();

        string html = await GetAsync("/Research/Sectors", vietnamese: true);

        Dictionary<string, string> strings = PageIsland(html, "Research.Js.");
        Assert.Equal(TestLocalization.Vietnamese("Research.Js.Page.Pause"), strings["Research.Js.Page.Pause"]);
    }

    [Fact]
    public async Task NewsPage_InVietnamese_ShowsNoEnglishChrome()
    {
        _news.Returns(nameof(INewsApi.GetNewsAsync), Feed);

        string english = await GetAsync("/News", vietnamese: false);
        string html = await GetAsync("/News", vietnamese: true);

        AssertShows(english, NewsEnglish);
        AssertHides(html, NewsEnglish);
        Assert.Contains(TestLocalization.Vietnamese("News.Title"), html);
        Assert.Contains("Markets close higher", html);
        Assert.Contains("Investing", html);
    }

    [Fact]
    public async Task NewsPage_WithNoArticles_InVietnamese_ShowsNoEnglishEmptyState()
    {
        _news.Returns(nameof(INewsApi.GetNewsAsync), new NewsFeedDto());

        string english = await GetAsync("/News", vietnamese: false);
        string html = await GetAsync("/News", vietnamese: true);

        AssertShows(english, NewsEmptyEnglish);
        AssertHides(html, NewsEmptyEnglish);
    }

    [Fact]
    public async Task NewsPage_WhenTheFeedFails_InVietnamese_ShowsTheErrorInVietnamese()
    {
        string html = await GetAsync("/News", vietnamese: true);

        Assert.DoesNotContain("Unable to load", html);
        Assert.Contains(TestLocalization.Vietnamese("News.Message.LoadFailed"), html);
    }

    [Fact]
    public async Task NewsPage_CarriesTheNewsJsStrings_InTheirOwnIsland()
    {
        _news.Returns(nameof(INewsApi.GetNewsAsync), Feed);

        string html = await GetAsync("/News", vietnamese: true);

        Dictionary<string, string> strings = PageIsland(html, "News.Js.");
        Assert.Equal(TestLocalization.Vietnamese("News.Js.Watchlist.Empty"), strings["News.Js.Watchlist.Empty"]);
    }

    private void LoadEveryDataset()
    {
        _research.Returns(nameof(IResearchApi.GetSectorsAsync), Sectors)
            .Returns(nameof(IResearchApi.GetFundsAsync), Funds)
            .Returns(nameof(IResearchApi.GetWorldAsync), World)
            .Returns(nameof(IResearchApi.GetFundOverlapAsync), Overlap)
            .Returns(nameof(IResearchApi.GetFundReturnsAsync), Returns)
            .Returns(nameof(IResearchApi.GetMacroAsync), Macro)
            .Returns(nameof(IResearchApi.GetFundFactsAsync), new FundFactsSnapshotDto())
            .Returns(nameof(IResearchApi.GetFundExposureAsync), Exposure);
    }

    private async Task<string> GetAsync(string url, bool vietnamese)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (vietnamese)
        {
            request.Headers.Add("Cookie", VietnameseCookie);
        }

        request.Headers.Add(SignedInHeader, "1");
        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    private static void AssertShows(string html, IEnumerable<string> texts) =>
        Assert.All(texts, text => Assert.Contains(text, html, StringComparison.Ordinal));

    // What a visitor can see or hear: markup and attributes, without HTML comments or script bodies (the JSON islands'
    // key names are English by design).
    private static void AssertHides(string html, IEnumerable<string> texts)
    {
        string shown = NotShown().Replace(html, string.Empty);
        Assert.All(texts, text => Assert.DoesNotContain(text, shown, StringComparison.Ordinal));
    }

    private static Dictionary<string, string> PageIsland(string html, string prefix)
    {
        Dictionary<string, string> island = Island().Matches(html)
            .Select(m => JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[1].Value)!)
            .Single(strings => strings.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.All(island.Keys, key => Assert.StartsWith(prefix, key));
        return island;
    }

    [GeneratedRegex("<!--.*?-->|<script[^>]*>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex NotShown();

    [GeneratedRegex("""<script type="application/json" data-i18n-strings(?:="")?>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex Island();

    /// <summary>Signs the request in as an Account when it carries the test header, as Identity's cookie would.</summary>
    private sealed class HeaderSignInHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(SignedInHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "research-user"),
                new Claim(ClaimTypes.Email, "research@example.com")
            ], IdentityConstants.ApplicationScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme)));
        }
    }
}
