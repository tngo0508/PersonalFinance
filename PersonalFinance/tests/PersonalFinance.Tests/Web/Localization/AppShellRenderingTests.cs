using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The app shell (slice L3, #357) rendered through the real compiled Razor views: the layout's navigation, top bar,
/// footer and mobile bottom nav, <c>_LoginPartial</c>, <c>_CookieBanner</c>, <c>_DataSourceToggle</c>, the Unavailable
/// State and the Send feedback page show no English chrome when the UI language is Vietnamese, and carry the shell's
/// JS strings in their own island.
/// </summary>
public sealed partial class AppShellRenderingTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Den-US%7Cuic%3Dvi";
    private const string SignedInHeader = "X-Test-Signed-In";

    // Visible English chrome of the shell. Each must render in English, and none may render in Vietnamese.
    private static readonly string[] SignedInEnglish =
    [
        "Skip to main content", "Main navigation", "Overview", "My Finances", "Net Worth", "Budget Tracker",
        "Budget Planner", "Money Priorities", "Investment Calculator", "Finance News", "Send feedback",
        "Search transactions, categories, goals...", "Notifications", "Account settings", "Sign out",
        "Privacy Policy", "Terms of Service", ">Disclaimer<", "Mobile navigation", "Finances", "Tools", "Profile",
        "Accept All", "Essential Only", "We use essential cookies", "Learn more", "Version "
    ];

    private static readonly string[] SignedOutEnglish = ["Sign in", "Create account", "Privacy Policy", "Accept All"];

    private static readonly string[] UnavailableEnglish = ["Your data is safe.", ">Retry<"];

    private static readonly string[] DataSourceEnglish = ["Data source:", "Budget data source selection"];

    private static readonly string[] FeedbackEnglish =
    [
        "Found a bug or have an idea?", "Contact page", ">Category<", ">Bug<", ">Idea<", ">Message<",
        "characters.", "Sent from page:"
    ];

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
            .AddApplicationPart(typeof(FeedbackController).Assembly)
            .AddApplicationPart(typeof(ShellPreviewController).Assembly);

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
    public async Task SignedInShell_InEnglish_ShowsItsChrome()
    {
        string html = await GetAsync("/ShellPreview/Unavailable", signedIn: true, vietnamese: false);

        AssertShows(html, SignedInEnglish.Concat(UnavailableEnglish));
    }

    // The shell's navigation reaches every feature page. That each page's controller exists is NavigationLayoutTests'
    // job: link generation here renders the href even for a controller MVC cannot route to.
    [Fact]
    public async Task SignedInShell_LinksToEveryFeaturePage()
    {
        string html = await GetAsync("/ShellPreview/Unavailable", signedIn: true, vietnamese: false);

        Assert.All(
        [
            "/Dashboard", "/Networth", "/BudgetTracker", "/Budget", "/SavingsGoals", "/MoneyPriorities", "/Analysis",
            "/InvestmentCalculator", "/News", "/Community", "/GoogleDrive", "/Research"
        ], page => Assert.Contains($"href=\"{page}\"", html, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignedInShell_InVietnamese_ShowsNoEnglishChrome()
    {
        string html = await GetAsync("/ShellPreview/Unavailable", signedIn: true, vietnamese: true);

        AssertHides(html, SignedInEnglish.Concat(UnavailableEnglish));
        Assert.Contains(TestLocalization.Vietnamese("Shell.Nav.Dashboard"), html);
        Assert.Contains(TestLocalization.Vietnamese("Shell.Account.SignOut"), html);
    }

    [Fact]
    public async Task SignedOutShell_InVietnamese_ShowsNoEnglishChrome()
    {
        string english = await GetAsync("/ShellPreview/Unavailable", signedIn: false, vietnamese: false);
        string html = await GetAsync("/ShellPreview/Unavailable", signedIn: false, vietnamese: true);

        AssertShows(english, SignedOutEnglish);
        AssertHides(html, SignedOutEnglish);
    }

    [Fact]
    public async Task DataSourceToggle_InVietnamese_ShowsNoEnglish()
    {
        string english = await GetAsync("/ShellPreview/DataSource", signedIn: true, vietnamese: false);
        string html = await GetAsync("/ShellPreview/DataSource", signedIn: true, vietnamese: true);

        AssertShows(english, DataSourceEnglish);
        AssertHides(html, DataSourceEnglish);
    }

    [Fact]
    public async Task FeedbackPage_InVietnamese_ShowsNoEnglish()
    {
        string english = await GetAsync("/Feedback?from=%2FBudget", signedIn: true, vietnamese: false);
        string html = await GetAsync("/Feedback?from=%2FBudget", signedIn: true, vietnamese: true);

        AssertShows(english, FeedbackEnglish);
        AssertHides(html, FeedbackEnglish);
        Assert.Contains(TestLocalization.Vietnamese("Feedback.Title"), html);
    }

    [Fact]
    public async Task SignedInShell_CarriesTheShellJsStrings_InTheirOwnIsland()
    {
        string html = await GetAsync("/ShellPreview/Unavailable", signedIn: true, vietnamese: true);

        Dictionary<string, string> strings = Islands(html).SelectMany(island => island).ToDictionary();
        Assert.Equal(TestLocalization.Vietnamese("Shell.Js.Search.Unavailable"), strings["Shell.Js.Search.Unavailable"]);
        Assert.Equal(TestLocalization.Vietnamese("Shell.Js.Notifications.Empty"), strings["Shell.Js.Notifications.Empty"]);
        Assert.All(strings.Keys, key => Assert.StartsWith("Shell.Js.", key));
    }

    private async Task<string> GetAsync(string url, bool signedIn, bool vietnamese)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (vietnamese)
        {
            request.Headers.Add("Cookie", VietnameseCookie);
        }

        if (signedIn)
        {
            request.Headers.Add(SignedInHeader, "1");
        }

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    private static void AssertShows(string html, IEnumerable<string> texts) =>
        Assert.All(texts, text => Assert.Contains(text, html, StringComparison.Ordinal));

    // What a visitor can see or hear: markup and attributes, without HTML comments or script bodies (the JSON island's
    // key names are English by design).
    private static void AssertHides(string html, IEnumerable<string> texts)
    {
        string shown = NotShown().Replace(html, string.Empty);
        Assert.All(texts, text => Assert.DoesNotContain(text, shown, StringComparison.Ordinal));
    }

    [GeneratedRegex("<!--.*?-->|<script[^>]*>.*?</script>", RegexOptions.Singleline)]
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
                new Claim(ClaimTypes.NameIdentifier, "shell-user"),
                new Claim(ClaimTypes.Email, "shell@example.com"),
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

/// <summary>Renders shared shell views that no single page owns, for <see cref="AppShellRenderingTests"/>.</summary>
public sealed class ShellPreviewController : Controller
{
    public IActionResult Unavailable() =>
        this.UnavailableView("Savings Goals", "We couldn't load your savings goals", "SavingsGoals");

    public IActionResult DataSource() => PartialView("_DataSourceToggle", BudgetDataSource.GoogleSheet);
}
