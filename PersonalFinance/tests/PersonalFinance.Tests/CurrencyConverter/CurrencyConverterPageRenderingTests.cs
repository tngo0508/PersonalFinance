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
using PersonalFinance.Shared.DTOs.CurrencyConverter;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.CurrencyConverter;

/// <summary>
/// The Currency Converter page (#446, #490) rendered through the real compiled Razor view, as it shows without
/// JavaScript: no result before the first submit; the converted amount in the To field, "1 {From} = {rate} {To}" and
/// its inverse, the rate date and the source after one; the "rate as of" notice when the rate is stale; "Conversion
/// unavailable" and an empty To field when there is no rate.
/// </summary>
public sealed class CurrencyConverterPageRenderingTests : IAsyncLifetime
{
    private const string SubmitUrl = "/CurrencyConverter?amount=1000000&from=JPY&to=VND";
    private readonly CurrencyConverterWebControllerTests.FakeConverterApi _api = new();
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
        builder.Services.AddSingleton<ICurrencyConverterApi>(_api);
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
            .AddApplicationPart(typeof(CurrencyConverterController).Assembly);

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
    public async Task BeforeTheFirstSubmit_ShowsTheFormAndNoResult()
    {
        string html = await GetAsync("/CurrencyConverter");

        Assert.Contains("id=\"currency-converter-form\"", html, StringComparison.Ordinal);
        Assert.Contains("Japanese yen (JPY)", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", Tag(html, "data-converter-result"), StringComparison.Ordinal);
        Assert.DoesNotContain("value=", Tag(html, "data-converter-amount"), StringComparison.Ordinal);
        Assert.False(IsShown(html, "data-converter-rate"));
        Assert.False(IsShown(html, "data-converter-unavailable"));
        Assert.True(IsShown(html, "id=\"converter-submit\""));
    }

    [Fact]
    public async Task Result_ShowsTheAmount_TheRate_ItsInverse_TheRateDate_AndTheSource()
    {
        _api.Result = Conversion(isStale: false);

        string html = await GetAsync(SubmitUrl);

        Assert.Contains("value=\"170000000\"", Tag(html, "data-converter-amount"), StringComparison.Ordinal);
        Assert.Contains("1,000,000 JPY = ₫170,000,000", html, StringComparison.Ordinal);
        Assert.Contains("1 JPY = 164.213 VND", html, StringComparison.Ordinal);
        Assert.Contains("1 VND = 0.00608963 JPY", html, StringComparison.Ordinal);
        Assert.Contains("Rate date 10/20/2026", html, StringComparison.Ordinal);
        Assert.Contains("Source: Frankfurter, currency-api", html, StringComparison.Ordinal);
        Assert.True(IsShown(html, "data-converter-rate"));
        Assert.False(IsShown(html, "data-converter-stale"));
        Assert.False(IsShown(html, "data-converter-unavailable"));
    }

    [Fact]
    public async Task StaleResult_AddsTheRateAsOfNotice()
    {
        _api.Result = Conversion(isStale: true);

        string html = await GetAsync(SubmitUrl);

        Assert.True(IsShown(html, "data-converter-stale"));
        Assert.Contains("Rate as of 10/20/2026.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableResult_SaysSo_AndShowsNoAmount()
    {
        _api.Result = new CurrencyConversionDto { Amount = 1_000_000m, From = "JPY", To = "VND", Unavailable = true };

        string html = await GetAsync(SubmitUrl);

        Assert.True(IsShown(html, "data-converter-unavailable"));
        Assert.Contains(TestLocalization.Localizer["CurrencyConverter.Result.Unavailable"].Value, html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=", Tag(html, "data-converter-amount"), StringComparison.Ordinal);
        Assert.False(IsShown(html, "data-converter-rate"));
    }

    /// <summary>The opening tag of the first element carrying <paramref name="attribute"/>.</summary>
    private static string Tag(string html, string attribute)
    {
        Match match = Regex.Match(html, $"<[^>]*{Regex.Escape(attribute)}(?=[\\s>=/])[^>]*>");
        Assert.True(match.Success, $"No element with {attribute}.");
        return match.Value;
    }

    /// <summary>Whether the element carrying <paramref name="attribute"/> is rendered without <c>hidden</c>.</summary>
    private static bool IsShown(string html, string attribute) =>
        !Tag(html, attribute).Contains("hidden=\"hidden\"", StringComparison.Ordinal);

    private static CurrencyConversionDto Conversion(bool isStale) => new()
    {
        Amount = 1_000_000m,
        From = "JPY",
        To = "VND",
        ConvertedAmount = 170_000_000m,
        Rate = 164.2134725m,
        RateDate = new DateOnly(2026, 10, 20),
        Sources = ["frankfurter", "currency-api"],
        IsStale = isStale
    };

    private async Task<string> GetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept-Language", "en-US");
        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {response.StatusCode}: {html}");
        return WebUtility.HtmlDecode(html);
    }

    /// <summary>Signs every request in as an Account, as Identity's cookie would.</summary>
    private sealed class SignedInHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "converter-user"),
                new Claim(ClaimTypes.Email, "converter@example.com")
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
