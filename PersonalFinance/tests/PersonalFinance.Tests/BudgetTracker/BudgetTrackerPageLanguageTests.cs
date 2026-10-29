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
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// The Budget Tracker page (slice L3, #362) rendered through the real compiled Razor view: no English chrome when the
/// UI language is Vietnamese, amounts formatted by the money formatter in the request culture, and every string the
/// page script needs carried in its <c>BudgetTracker.Js.</c> island. It also guards the page's single month navigator
/// and single Add Category per section (Reduce Clutter, #454).
/// </summary>
public sealed partial class BudgetTrackerPageLanguageTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi%7Cuic%3Dvi";
    private const string SignedInHeader = "X-Test-Signed-In";

    private static readonly string ScriptPath = Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"),
        "wwwroot", "js", "budget-tracker", "budget-tracker.js");

    // Visible English chrome of the page. Each must render in English, and none may render in Vietnamese.
    private static readonly string[] PageEnglish =
    [
        "Manage your monthly planned budget", "Month navigation", "Previous Month", "Next Month", ">Month<", ">Year<",
        "New Month Budget Plan", "This month has no budget plan yet", "Copy from Previous Month", "Start Blank",
        "Expenses Comparison", "Income Comparison", "Planned: ", "Planned Expenses", "Actual Income",
        "Add Custom Expense Category", "Add Category", "Expenses Summary Table", ">Planned<", ">Actual<", ">Diff<",
        ">Actions<", "Total Expenses", "Total Income", "Record and itemize", "Add Transaction",
        "Expense Transactions Table", ">Amount<", ">Description<", "No expense transactions recorded",
        "No income transactions recorded", "Add Budget Category", "Category Name", "e.g. Subscriptions",
        "Category Type", "Rename Category", "e.g. Groceries", "Category type cannot be changed", "Save Changes",
        "Set a manual Actual amount", "This overrides the sum of transactions", "Manual Actual Amount",
        "Revert to Transactions", "Save Override", "Select a category...", "Description (Optional)",
        "e.g. Grocery shopping", "Save Transaction", "Confirm Deletion", "Are you sure you want to delete this item?",
        "Loading..."
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
        builder.Services.AddSingleton<IBudgetTrackerApi>(new FakeBudgetTrackerApi
        {
            Sheet = new MonthlyBudgetSheetDto { Year = 2026, Month = 10, IsInitialized = false }
        });
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
            .AddApplicationPart(typeof(BudgetTrackerController).Assembly);

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
    public async Task Page_InEnglish_ShowsItsChrome()
    {
        string html = await GetAsync(vietnamese: false);

        Assert.All(PageEnglish, text => Assert.Contains(text, html, StringComparison.Ordinal));
        Assert.Contains("$0.00", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_InVietnamese_ShowsNoEnglishChrome()
    {
        string html = await GetAsync(vietnamese: true);

        string shown = NotShown().Replace(html, string.Empty);
        Assert.All(PageEnglish, text => Assert.DoesNotContain(text, shown, StringComparison.Ordinal));
        Assert.Contains(TestLocalization.Vietnamese("BudgetTracker.Title"), html);
        Assert.Contains(TestLocalization.Vietnamese("BudgetTracker.Tabs.Summary"), html);
    }

    [Fact]
    public async Task Page_InVietnamese_FormatsAmountsInTheVietnameseCulture()
    {
        string html = await GetAsync(vietnamese: true);

        string zero = MoneyFormatter.Format(0m, SupportedCurrencies.Default.Code, CultureInfo.GetCultureInfo("vi-VN"));
        Assert.Contains(zero, html, StringComparison.Ordinal);
        Assert.DoesNotContain("$0.00", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_CarriesEveryStringTheScriptUses_InItsIsland()
    {
        string html = await GetAsync(vietnamese: true);

        Dictionary<string, string> strings = Islands(html).SelectMany(island => island).ToDictionary();
        List<string> scriptKeys = ScriptKey().Matches(File.ReadAllText(ScriptPath))
            .Select(m => m.Groups["key"].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(scriptKeys);
        Assert.All(scriptKeys, key =>
        {
            Assert.True(strings.ContainsKey(key), $"The page's island has no {key}.");
            Assert.Equal(TestLocalization.Vietnamese(key), strings[key]);
        });
    }

    // Reduce Clutter (#454): each action has one entry point on the page.
    [Fact]
    public async Task Page_HasOneMonthNavigator_AndOneAddCategoryPerSection()
    {
        string html = NotShown().Replace(await GetAsync(vietnamese: false), string.Empty);

        Assert.Equal(1, Regex.Count(html, "class=\"bt-month-nav\""));
        Assert.Equal(1, Regex.Count(html, "id=\"btnPrevMonth\""));
        Assert.Equal(1, Regex.Count(html, "id=\"btnNextMonth\""));
        Assert.Equal(1, Regex.Count(html, "id=\"btnAddExpenseCategory\""));
        Assert.Equal(1, Regex.Count(html, "id=\"btnAddIncomeCategory\""));
        // Outside the modals, only the expense and income section headers offer Add Category.
        string page = html[..html.IndexOf("class=\"modal fade\"", StringComparison.Ordinal)];
        Assert.Equal(2, Regex.Count(page, @">\s*Add Category\s*</button>"));
    }

    private async Task<string> GetAsync(bool vietnamese)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/BudgetTracker?year=2026&month=10");
        if (vietnamese)
        {
            request.Headers.Add("Cookie", VietnameseCookie);
        }

        request.Headers.Add(SignedInHeader, "1");

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"The page returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    // What a visitor can see or hear: markup and attributes, without HTML comments or script bodies (the JSON island's
    // key names are English by design).
    [GeneratedRegex("<!--.*?-->|<script[^>]*>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex NotShown();

    private static IEnumerable<Dictionary<string, string>> Islands(string html) =>
        Island().Matches(html).Select(m => JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[1].Value)!);

    [GeneratedRegex("""<script type="application/json" data-i18n-strings(?:="")?>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex Island();

    // PFI18n.t('BudgetTracker.Js.Key', ...) and PFI18n.plural('BudgetTracker.Js.Key', ...): plural keys add .one/.other.
    [GeneratedRegex(@"PFI18n\.t\(\s*'(?<key>BudgetTracker\.Js\.[A-Za-z0-9.]+)'")]
    private static partial Regex ScriptKey();

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
                new Claim(ClaimTypes.NameIdentifier, "tracker-user"),
                new Claim(ClaimTypes.Email, "tracker@example.com"),
                new Claim(ClaimTypes.GivenName, "Linh")
            ], IdentityConstants.ApplicationScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme)));
        }
    }
}
