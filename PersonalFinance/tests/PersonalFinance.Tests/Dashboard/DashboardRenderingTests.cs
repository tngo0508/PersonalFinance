using System.Globalization;
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
using PersonalFinance.Shared.DTOs.Dashboard;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Dashboard;

/// <summary>
/// The Dashboard (slice L3, #360) rendered through the real compiled Razor views: with data, with every section empty,
/// and in the Unavailable State, it shows its chrome in English and none of it when the UI language is Vietnamese.
/// Money and dates follow the request culture; the chart scripts get their strings from a <c>Dashboard.Js.</c> island.
/// </summary>
public sealed partial class DashboardRenderingTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";
    private const string SignedInHeader = "X-Test-Signed-In";

    // Visible English of a Dashboard with data in every section.
    private static readonly string[] FilledEnglish =
    [
        "Welcome back, Linh", "Here is where your finances stand today.", ">Today<", "Edit settings", "From your Net Worth settings.",
        "Progress to FI", "annual spending × 25", "On the Base scenario you reach your FI target at age",
        "Key figures", "Budgeted income", "Goals funded", "Savings Rate", "Of annual income saved", "1 of 2 complete",
        "Per month · October 2026 (Budget Planner)",
        "Net worth projection", "using your Net Worth settings", "Base at 65", "Base scenario reaches",
        "Monthly budget", "Planned income", "Actual income", "Planned expenses", "Actual expenses", "Open Budget Tracker",
        "Planned versus actual income and expenses", "Source: Budget Tracker",
        "Edit budget", "monthly income", "Planned split of monthly income", "Housing", "Debt Repayment",
        "Allocations total", "more than you earn",
        "Savings goals", "View all", "Complete\n", "% funded", "Emergency fund progress",
        "Recent activity", "Target:", "| Saved:", ">Income<", ">Savings goal<", "5d ago", "Oct 01, 2026",
        "Quick actions", "Update balances and your FI projection", "Plan this month's spending split",
        "Track progress toward a goal", "Review trends in your spending", "Model growth from regular contributions",
        "Finish setting up", "Connect Google Drive", "Link a Drive folder"
    ];

    // Visible English of a Dashboard for a user with no data, on the Google Sheet source.
    private static readonly string[] EmptyEnglish =
    [
        "Add your Net Worth settings to see your headline figure", "Add your Net Worth settings to see where your plan leads",
        "Not set up", "Add your Net Worth settings", "Create a budget", "Add a savings goal",
        "No data in Google Sheet.", "Connect Google Drive below", "to see your tracked transactions","or switch to Budget Tracker above.",
        "No budget plan set up yet.", "Create one below", "to plan your income allocation.",
        "Add a savings goal to track your progress here.", "No recent activity from Google Sheet.",
        "Set up net worth", "Enter your balance, income, and spending", "Plan how your monthly income is split",
        "Add a goal", "Track progress toward something you are saving for."
    ];

    private static readonly string[] UnavailableEnglish = ["We couldn't load your dashboard", "Welcome back"];

    private static readonly CultureInfo Vietnam = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly DashboardSummaryDto Filled = new()
    {
        NetWorth = new NetWorthSummaryDto
        {
            CurrentNetWorth = 125_000m, FiTarget = 1_000_000m, ProgressToFiPercent = 12.5m, SavingsRatePercent = 20m,
            CurrentAge = 35, RetirementAge = 65, FiReachedAge = 60,
            ProjectionPoints =
            [
                new ProjectionPointDto { Year = 2026, Age = 35, Base = 125_000m, Conservative = 125_000m, Aggressive = 125_000m },
                new ProjectionPointDto { Year = 2056, Age = 65, Base = 1_500_000m, Conservative = 900_000m, Aggressive = 2_100_000m }
            ]
        },
        Budget = new BudgetSummaryDto
        {
            MonthlyIncome = 6_500m, Period = "2026-10", TotalAllocatedPercent = 112.5m,
            Allocations =
            [
                new BudgetAllocationDto { Name = "Housing", Key = "housing", Percent = 100m, Amount = 6_500m },
                new BudgetAllocationDto { Name = "Debt Repayment", Key = "debt-repayment", Percent = 12.5m, Amount = 812.5m }
            ]
        },
        MonthlyBudget = new DashboardMonthlyBudgetDto
        {
            Year = 2026, Month = 10, Period = "October 2026", BudgetedIncome = 6_500m, ActualIncome = 6_400m,
            BudgetedExpenses = 5_000m, ActualExpenses = 5_100m
        },
        SavingsGoals =
        [
            new SavingsGoalSummaryDto { Id = 1, Name = "Emergency fund", TargetAmount = 10_000m, CurrentAmount = 10_000m, ProgressPercent = 100, IsCompleted = true },
            new SavingsGoalSummaryDto { Id = 2, Name = "Car", TargetAmount = 20_000m, CurrentAmount = 5_000m, ProgressPercent = 25 }
        ],
        GoalsOverview = new GoalsOverviewDto { TotalSaved = 15_000m, TotalTarget = 30_000m, FundedPercent = 50m, CompletedCount = 1, TotalCount = 2 },
        RecentActivity =
        [
            new RecentActivityDto
            {
                Title = "Salary", Description = "Paycheck - $6,400.00", Date = DateTime.UtcNow.Date.AddDays(-5), Type = "Income",
                Category = "Paycheck", Amount = 6_400m
            },
            new RecentActivityDto
            {
                Title = "Car", Description = "Target: $20,000.00 | Saved: $5,000.00", Date = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                Type = "SavingsGoal", TargetAmount = 20_000m, SavedAmount = 5_000m
            }
        ],
        ActiveSource = BudgetDataSource.BudgetTracker,
        HasGoogleDriveConnection = false
    };

    private readonly FakeDashboardApi _api = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = RepositoryRoot.SourceProject("PersonalFinance.Web"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddSingleton<IFeedbackApi, NoFeedbackApi>();
        builder.Services.AddSingleton<IDashboardApi>(_api);
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddIdentityCore<IdentityUser>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, HeaderSignInHandler>(IdentityConstants.ApplicationScheme, null);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddApplicationPart(typeof(DashboardController).Assembly);

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

    [Fact]
    public async Task FilledDashboard_InEnglish_ShowsItsChrome()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: false);

        AssertShows(html, FilledEnglish);
    }

    [Fact]
    public async Task FilledDashboard_InVietnamese_ShowsNoEnglishChrome()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: true);

        AssertHides(html, FilledEnglish);
        Assert.Contains(TestLocalization.Vietnamese("Dashboard.Header.Lede"), html);
    }

    [Fact]
    public async Task FilledDashboard_InVietnamese_FormatsMoneyPercentsAndDatesInTheCulture()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: true);

        Assert.Contains(MoneyFormatter.FormatWhole(125_000m, SupportedCurrencies.Default.Code, Vietnam), html);
        Assert.Contains(MoneyFormatter.Format(6_400m, SupportedCurrencies.Default.Code, Vietnam), html);
        Assert.Contains("12,5%", html);
        Assert.Contains(TestLocalization.Vietnamese("Common.TimeAgo.Days").Replace("{count}", "5", StringComparison.Ordinal), html);
        Assert.Contains(new DateTime(2026, 10, 1).ToString("d", Vietnam), html);
        Assert.Contains(new DateTime(2026, 10, 1).ToString("Y", Vietnam), html);
    }

    [Fact]
    public async Task EmptyDashboard_InEnglish_ShowsItsChrome()
    {
        _api.Summary = new DashboardSummaryDto();

        string html = await GetAsync(vietnamese: false);

        AssertShows(html, EmptyEnglish);
    }

    [Fact]
    public async Task EmptyDashboard_InVietnamese_ShowsNoEnglishChrome()
    {
        _api.Summary = new DashboardSummaryDto();

        string html = await GetAsync(vietnamese: true);

        AssertHides(html, EmptyEnglish);
    }

    [Fact]
    public async Task UnavailableDashboard_InVietnamese_ShowsNoEnglishChrome()
    {
        _api.Failure = new HttpRequestException("API unavailable");

        string english = await GetAsync(vietnamese: false);
        string html = await GetAsync(vietnamese: true);

        AssertShows(english, UnavailableEnglish);
        AssertHides(html, UnavailableEnglish);
    }

    [Fact]
    public async Task Dashboard_CarriesTheChartStrings_InItsOwnIsland()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: true);

        Dictionary<string, string> strings = Islands(html)
            .Where(island => island.Keys.Any(key => key.StartsWith("Dashboard.Js.", StringComparison.Ordinal)))
            .SelectMany(island => island)
            .ToDictionary();
        Assert.Equal(TestLocalization.Vietnamese("Dashboard.Js.Scenario.Base"), strings["Dashboard.Js.Scenario.Base"]);
        Assert.Equal(TestLocalization.Vietnamese("Dashboard.Js.PercentOfIncome"), strings["Dashboard.Js.PercentOfIncome"]);
        Assert.All(strings.Keys, key => Assert.StartsWith("Dashboard.Js.", key));
    }

    [Fact]
    public async Task EmptyDashboard_OffersEachSetupStepOnce_OnItsSetupCard()
    {
        _api.Summary = new DashboardSummaryDto();

        string html = await GetAsync(vietnamese: false);

        Assert.Equal(["/Budget", "/GoogleDrive", "/Networth", "/SavingsGoals"], CardLinks(html).Order());
        Assert.Equal(4, CardLinks(Section(html, "dash-setup")).Count);
    }

    [Fact]
    public async Task ConnectedSheetWithNoData_OffersSyncOnce_OnTheMonthlyBudgetCard()
    {
        _api.Summary = new DashboardSummaryDto { HasGoogleDriveConnection = true };

        string html = await GetAsync(vietnamese: false);

        Assert.Single(CardLinks(html), "/GoogleDrive");
        Assert.Equal(["/GoogleDrive"], CardLinks(Section(html, "dash-monthly-budget")));
        Assert.Empty(CardLinks(Section(html, "dash-activity")));
    }

    [Fact]
    public async Task FilledDashboard_LinksEachDestinationOnce()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: false);

        Assert.Equal(["/Budget", "/BudgetTracker", "/GoogleDrive", "/Networth", "/SavingsGoals"], CardLinks(html).Order());
        Assert.DoesNotContain("View details", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetWorthWithoutSpending_SummaryOffersOnlyTheSpendingPrompt()
    {
        _api.Summary = new DashboardSummaryDto { NetWorth = new NetWorthSummaryDto { CurrentNetWorth = 10_000m } };

        string html = await GetAsync(vietnamese: false);

        Assert.Equal(["/Networth"], CardLinks(Section(html, "dash-summary")));
        Assert.DoesNotContain("Edit settings", Section(html, "dash-summary"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuickActions_AreMarkedMobileOnly()
    {
        _api.Summary = Filled;

        string html = await GetAsync(vietnamese: false);

        Assert.Contains("class=\"dash-card dash-actions dash-actions--mobile\" aria-labelledby=", html, StringComparison.Ordinal);
    }

    // The hrefs of the Dashboard's action links: the sidebar, key-figure tiles (content, not buttons) and the
    // mobile-only quick actions (they stand in for the sidebar) are left out.
    private static List<string> CardLinks(string html)
    {
        int start = html.IndexOf("<div class=\"dash\">", StringComparison.Ordinal);
        int end = html.IndexOf("id=\"dashboard-data\"", StringComparison.Ordinal);
        string dash = NotACardLink().Replace(html[Math.Max(start, 0)..(end < 0 ? html.Length : end)], string.Empty);
        return LinkHref().Matches(dash).Select(m => m.Groups[1].Value).ToList();
    }

    private static string Section(string html, string className)
    {
        Match match = Regex.Match(html, $"<section class=\"[^\"]*\\b{className}\\b[^\"]*\".*?</section>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No section .{className}");
        return match.Value;
    }

    [GeneratedRegex("""<a\b[^>]*class="dash-tile"[^>]*>.*?</a>|<section class="dash-card dash-actions[^"]*".*?</section>|<script\b.*?</script>""", RegexOptions.Singleline)]
    private static partial Regex NotACardLink();

    [GeneratedRegex("<a\\b[^>]*\\bhref=\"([^\"]*)\"")]
    private static partial Regex LinkHref();

    private async Task<string> GetAsync(bool vietnamese)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard");
        if (vietnamese)
        {
            request.Headers.Add("Cookie", VietnameseCookie);
        }

        request.Headers.Add(SignedInHeader, "1");

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"/Dashboard returned {response.StatusCode}: {html}");
        // A Windows checkout renders the views with CRLF; the expectations are written with LF.
        return WebUtility.HtmlDecode(html).ReplaceLineEndings("\n");
    }

    private static void AssertShows(string html, IEnumerable<string> texts) =>
        Assert.All(texts, text => Assert.Contains(text, html, StringComparison.Ordinal));

    // What a visitor can see or hear: markup and attributes, without HTML comments or script bodies.
    private static void AssertHides(string html, IEnumerable<string> texts)
    {
        string shown = NotShown().Replace(html, string.Empty);
        Assert.All(texts, text => Assert.DoesNotContain(text, shown, StringComparison.Ordinal));
    }

    [GeneratedRegex("<!--.*?-->|<script[^>]*>.*?</script>|@\\*.*?\\*@", RegexOptions.Singleline)]
    private static partial Regex NotShown();

    private static IEnumerable<Dictionary<string, string>> Islands(string html) =>
        Island().Matches(html).Select(m => JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[1].Value)!);

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
                new Claim(ClaimTypes.NameIdentifier, "dashboard-user"),
                new Claim(ClaimTypes.Email, "dashboard@example.com"),
                new Claim(ClaimTypes.GivenName, "Linh")
            ], IdentityConstants.ApplicationScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme)));
        }
    }

    private sealed class NoFeedbackApi : IFeedbackApi
    {
        public Task SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<List<FeedbackAdminItemDto>> ListAsync(FeedbackStatus? status = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<FeedbackAdminItemDto>());

        public Task ChangeStatusAsync(int id, ChangeFeedbackStatusRequest request,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
