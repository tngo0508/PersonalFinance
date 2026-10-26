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
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Networth;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The Net Worth page and the Investment Calculator (slice L3, #363) rendered through the real compiled Razor views:
/// in Vietnamese they show no English text, format amounts in vi-VN, and carry their JS strings in their own island.
/// </summary>
public sealed partial class NetWorthPagesRenderingTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";
    private const char Nbsp = ' ';

    // Visible English text of the Net Worth page. Each must render in English, and none may render in Vietnamese.
    private static readonly string[] NetWorthEnglish =
    [
        "Net Worth &amp; FI Projection", "Net Worth & FI Projection", "Explore market research", "How it works",
        "Quick Presets", "Coast FI", "Savings Rate", "Financial Overview", "Starting Balance",
        "Your current total investable assets", "Annual Spending", "Return Assumptions", "Conservative ROI",
        "Randomize", "Reset to saved", "Clear saved parameters", "FI Target (4% Rule)", "Current Net Worth",
        "Base Final Net Worth", "/yr spending", "How this is calculated", ">from<", "Net Worth Growth Over Time",
        "Final Net Worth Comparison", "Base Scenario", "Total Contributions", "Interest Earned",
        "Year-by-Year Projection", "Scenario selection", "milestone", "ROI Earned", "Ending Balance",
        "How Net Worth Projection Works", "What is the 4% Rule?", "Three Scenarios", "Reading the Charts",
        "gold star", "Got it", ">Close<", "aria-label=\"Close\""
    ];

    private static readonly string[] CalculatorEnglish =
    [
        "Compound Growth Calculator", "See how your investments", "Investment Amount", "Initial Investment",
        "Monthly Contribution", "Growth Parameters", "Annual Return Rate", "Time Period", "20 years",
        "Future Value", "Total Contributions", "Interest Earned", "Growth Over Time", "Total Value",
        "Investment growth chart", "Initial investment amount"
    ];

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        (INetWorthApi netWorthApi, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Returns(nameof(INetWorthApi.LoadParametersAsync), null);
        proxy.Returns(nameof(INetWorthApi.CalculateProjectionAsync),
            NetWorthProjectionService.ComputeProjection(new NetWorthViewModel().ToProjectionRequest()));

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = RepositoryRoot.SourceProject("PersonalFinance.Web"),
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(netWorthApi);
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddSingleton<IFeedbackApi, NoFeedbackApi>();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddIdentityCore<IdentityUser>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, SignedInHandler>(IdentityConstants.ApplicationScheme, null);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        builder.Services.AddAppLocalization(builder.Environment);
        builder.Services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddApplicationPart(typeof(NetworthController).Assembly);

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
    public async Task NetWorth_InEnglish_ShowsItsText()
    {
        string html = await GetAsync("/Networth", vietnamese: false);

        Assert.Contains("Net Worth & FI Projection", html, StringComparison.Ordinal);
        Assert.Contains("How Net Worth Projection Works", html, StringComparison.Ordinal);
        Assert.Contains("$1,250,000", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetWorth_InVietnamese_ShowsNoEnglish()
    {
        string html = await GetAsync("/Networth", vietnamese: true);

        AssertHides(html, NetWorthEnglish);
        Assert.Contains(TestLocalization.Vietnamese("NetWorth.Header.Title"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("NetWorth.Breakdown.Title"), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetWorth_InVietnamese_FormatsTheFiTargetInVietnamese()
    {
        string html = await GetAsync("/Networth", vietnamese: true);

        // The defaults spend 50,000 a year: an FI Target of 1,250,000, symbol after the number in vi-VN.
        Assert.Matches(new Regex($@"nw-metric-fi-value"">\s*1\.250\.000{Nbsp}\$\s*<"), html);
    }

    [Fact]
    public async Task NetWorth_CarriesItsJsStrings_InItsOwnIsland()
    {
        string html = await GetAsync("/Networth", vietnamese: true);

        Dictionary<string, string> strings = Island(html, "NetWorth.Js.");
        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Js.LoadFailed"), strings["NetWorth.Js.LoadFailed"]);
        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Js.Scenario.Aggressive"), strings["NetWorth.Js.Scenario.Aggressive"]);
    }

    [Fact]
    public async Task InvestmentCalculator_InEnglish_ShowsItsText()
    {
        string html = await GetAsync("/InvestmentCalculator", vietnamese: false);

        Assert.All(CalculatorEnglish, text => Assert.Contains(text, html, StringComparison.Ordinal));
        Assert.Contains("$10,000", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvestmentCalculator_InVietnamese_ShowsNoEnglish()
    {
        string html = await GetAsync("/InvestmentCalculator", vietnamese: true);

        AssertHides(html, CalculatorEnglish);
        Assert.Contains($"10.000{Nbsp}$", html, StringComparison.Ordinal);
        Dictionary<string, string> strings = Island(html, "InvestmentCalculator.Js.");
        Assert.Equal(TestLocalization.Vietnamese("InvestmentCalculator.Js.Tooltip.Start"),
            strings["InvestmentCalculator.Js.Tooltip.Start"]);
    }

    private async Task<string> GetAsync(string url, bool vietnamese)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (vietnamese)
        {
            request.Headers.Add("Cookie", VietnameseCookie);
        }

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    // What a visitor can see or hear: markup and attributes, without HTML comments or script bodies (the JSON island's
    // key names are English by design).
    private static void AssertHides(string html, IEnumerable<string> texts)
    {
        string shown = NotShown().Replace(html, string.Empty);
        Assert.All(texts, text => Assert.DoesNotContain(text, shown, StringComparison.Ordinal));
    }

    /// <summary>The island whose keys all start with <paramref name="prefix"/>.</summary>
    private static Dictionary<string, string> Island(string html, string prefix) =>
        IslandPattern().Matches(html)
            .Select(m => JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[1].Value)!)
            .Single(island => island.Count > 0 && island.Keys.All(key => key.StartsWith(prefix, StringComparison.Ordinal)));

    [GeneratedRegex("<!--.*?-->|<script[^>]*>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex NotShown();

    [GeneratedRegex("""<script type="application/json" data-i18n-strings(?:="")?>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex IslandPattern();

    /// <summary>Signs every request in as an Account, as Identity's cookie would.</summary>
    private sealed class SignedInHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "nw-user"),
                new Claim(ClaimTypes.Email, "nw@example.com"),
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
