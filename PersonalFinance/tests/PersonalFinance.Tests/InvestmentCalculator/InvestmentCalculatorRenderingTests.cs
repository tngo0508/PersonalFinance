using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.InvestmentCalculator;

/// <summary>
/// The Investment Calculator page (#588) rendered through the real compiled Razor view: amounts are in the Account's
/// Reporting Currency, with the money sliders' ranges, steps and starting values scaled by the currency's money-bounds
/// scale; the return is an "assumed" return, never an expected one; and the educational-estimate note links to the
/// Disclaimer.
/// </summary>
public sealed partial class InvestmentCalculatorRenderingTests : IAsyncLifetime
{
    private const string UserId = "calculator-user";

    private readonly InMemoryUserPreferenceService _preferences = new();
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
        builder.Services.AddSingleton<IUserPreferenceService>(_preferences);
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
        builder.Services.AddSingleton<ILearningLibraryStatus>(new FixedLearningLibraryStatus(false));
        builder.Services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddApplicationPart(typeof(InvestmentCalculatorController).Assembly);

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
    public async Task InUsDollars_TheMoneySlidersKeepTheirDollarRanges()
    {
        string html = await GetPageAsync();

        Assert.Contains("data-currency=\"USD\"", Tag(html, "data-investment-calculator"), StringComparison.Ordinal);
        string initial = Tag(html, "id=\"icInitialRange\"");
        Assert.Contains("max=\"500000\"", initial, StringComparison.Ordinal);
        Assert.Contains("step=\"1000\"", initial, StringComparison.Ordinal);
        Assert.Contains("value=\"10000\"", initial, StringComparison.Ordinal);
        Assert.Contains("$10,000", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InDong_TheMoneySlidersAndStartingAmountsAreScaledToTheCurrency()
    {
        _preferences.ReportingCurrencies[UserId] = "VND";

        string html = await GetPageAsync();

        Assert.Contains("data-currency=\"VND\"", Tag(html, "data-investment-calculator"), StringComparison.Ordinal);
        string initial = Tag(html, "id=\"icInitialRange\"");
        Assert.Contains("max=\"12500000000\"", initial, StringComparison.Ordinal);
        Assert.Contains("step=\"25000000\"", initial, StringComparison.Ordinal);
        Assert.Contains("value=\"250000000\"", initial, StringComparison.Ordinal);
        string monthly = Tag(html, "id=\"icMonthlyRange\"");
        Assert.Contains("max=\"250000000\"", monthly, StringComparison.Ordinal);
        Assert.Contains("step=\"1250000\"", monthly, StringComparison.Ordinal);
        Assert.Contains("value=\"12500000\"", monthly, StringComparison.Ordinal);
        Assert.Contains("₫250,000,000", html, StringComparison.Ordinal);
        Assert.DoesNotContain("$10,000", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReturnIsAssumed_AndTheEstimateNoteLinksToTheDisclaimer()
    {
        string page = Calculator(await GetPageAsync());

        Assert.Contains("Assumed yearly return", page, StringComparison.Ordinal);
        Assert.Contains("not financial advice", page, StringComparison.Ordinal);
        Assert.Contains("href=\"/disclaimer\"", page, StringComparison.Ordinal);
        Assert.DoesNotMatch(PromisedReturn(), page);
    }

    [Fact]
    public async Task RatePresets_OfferTheMoneyPrioritiesRates()
    {
        string page = Calculator(await GetPageAsync());

        Assert.Contains("data-rate-preset=\"4\"", page, StringComparison.Ordinal);
        Assert.Contains("data-rate-preset=\"7\"", page, StringComparison.Ordinal);
        Assert.Contains("data-rate-preset=\"10\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inflation_IsAnAssumptionOfZeroToTenPercent_StartingAtTwoAndAHalf()
    {
        string page = Calculator(await GetPageAsync());

        string range = Tag(page, "id=\"icInflationRange\"");
        Assert.Contains("min=\"0\"", range, StringComparison.Ordinal);
        Assert.Contains("max=\"10\"", range, StringComparison.Ordinal);
        Assert.Contains("value=\"2.5\"", range, StringComparison.Ordinal);
        int more = page.IndexOf("class=\"ic-more\"", StringComparison.Ordinal);
        Assert.True(more >= 0 && page.IndexOf("id=\"icInflationRange\"", StringComparison.Ordinal) > more,
            "The inflation field is not inside More assumptions.");
    }

    [Fact]
    public async Task TodaysMoney_IsASwitchInTheSummaryBar_OffByDefault()
    {
        string page = Calculator(await GetPageAsync());

        string toggle = Tag(page, "id=\"icTodaysMoney\"");
        Assert.Contains("type=\"checkbox\"", toggle, StringComparison.Ordinal);
        Assert.Contains("role=\"switch\"", toggle, StringComparison.Ordinal);
        Assert.DoesNotContain("checked", toggle, StringComparison.Ordinal);
        Assert.Contains("<label class=\"form-check-label\" for=\"icTodaysMoney\">Today's money</label>", page,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task YearByYear_IsATableWithColumnHeaders()
    {
        string page = Calculator(await GetPageAsync());

        int table = page.IndexOf("id=\"icYearTable\"", StringComparison.Ordinal);
        Assert.True(table >= 0, "No year-by-year table.");
        string head = page[table..page.IndexOf("</thead>", table, StringComparison.Ordinal)];
        Assert.Equal(5, Regex.Count(head, "<th scope=\"col\""));
        Assert.Contains("Balance", head, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryElementId_IsUnique()
    {
        string page = Calculator(await GetPageAsync());

        string[] repeated = [.. Regex.Matches(page, "\\sid=\"([^\"]+)\"")
            .GroupBy(match => match.Groups[1].Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        Assert.Empty(repeated);
    }

    /// <summary>The opening tag of the first element carrying <paramref name="attribute"/>.</summary>
    private static string Tag(string html, string attribute)
    {
        Match match = Regex.Match(html, $"<[^>]*{Regex.Escape(attribute)}(?=[\\s>=/])[^>]*>");
        Assert.True(match.Success, $"No element with {attribute}.");
        return match.Value;
    }

    /// <summary>The calculator's own markup, from its root to the end of the main content.</summary>
    private static string Calculator(string html)
    {
        int start = html.IndexOf("data-investment-calculator", StringComparison.Ordinal);
        Assert.True(start >= 0, "The page has no calculator root.");
        int end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        return end > start ? html[start..end] : html[start..];
    }

    private async Task<string> GetPageAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/InvestmentCalculator");
        request.Headers.Add("Accept-Language", "en-US");
        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"The page returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    [GeneratedRegex(@"\b(expected|will earn)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PromisedReturn();

    /// <summary>Signs every request in as the same Account, as Identity's cookie would.</summary>
    private sealed class SignedInHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Email, "calculator@example.com")
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
