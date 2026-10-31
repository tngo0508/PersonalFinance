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
using PersonalFinance.Shared.DTOs.SavingsGoals;
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
    private readonly GoalsApi _goals = new();
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
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero)));
        builder.Services.AddSingleton<ISavingsGoalsApi>(_goals);
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
    public async Task GoalTile_SolvesForTheMonthlyAmountByDefault_BeforeTheYearByYearTable()
    {
        string page = Calculator(await GetPageAsync());

        string monthly = Tag(page, "id=\"icGoalModeMonthly\"");
        Assert.Contains("type=\"radio\"", monthly, StringComparison.Ordinal);
        Assert.Contains("checked", monthly, StringComparison.Ordinal);
        Assert.DoesNotContain("checked", Tag(page, "id=\"icGoalModeYears\""), StringComparison.Ordinal);
        Assert.Contains("How much a month?", page, StringComparison.Ordinal);
        Assert.Contains("How long?", page, StringComparison.Ordinal);

        string progress = Tag(page, "id=\"icGoalProgress\"");
        Assert.Contains("role=\"progressbar\"", progress, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"100\"", progress, StringComparison.Ordinal);
        Assert.Contains("hidden", Tag(page, "id=\"icGoalUse\""), StringComparison.Ordinal);

        int goal = page.IndexOf("id=\"icGoal\"", StringComparison.Ordinal);
        Assert.True(goal >= 0 && goal < page.IndexOf("id=\"icYearly\"", StringComparison.Ordinal),
            "The goal tile is not before the year-by-year table.");
    }

    [Theory]
    [InlineData("USD", "500000", "1000000000")]
    [InlineData("VND", "12500000000", "25000000000000")]
    public async Task GoalAmount_StartsAndIsBoundedOnTheMoneyScale(string currency, string value, string max)
    {
        _preferences.ReportingCurrencies[UserId] = currency;

        string target = Tag(Calculator(await GetPageAsync()), "id=\"icGoal\"");

        Assert.Contains("type=\"number\"", target, StringComparison.Ordinal);
        Assert.Contains("min=\"0\"", target, StringComparison.Ordinal);
        Assert.Contains($"value=\"{value}\"", target, StringComparison.Ordinal);
        Assert.Contains($"max=\"{max}\"", target, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingsGoalPicker_ListsActiveGoals_WithTheirTargetAndYearsToTheTargetDate()
    {
        _goals.Goals.AddRange(
        [
            Goal(1, "Emergency fund", 25_000m, new DateTime(2029, 6, 1)),
            Goal(2, "New car", 18_000m, targetDate: null),
            Goal(3, "Paid off", 5_000m, new DateTime(2027, 1, 1), completed: true),
            Goal(4, "Retirement", 900_000m, new DateTime(2100, 1, 1)),
            Goal(5, "Overdue trip", 3_000m, new DateTime(2025, 1, 1))
        ]);

        string page = Calculator(await GetPageAsync());

        Assert.Contains("<label class=\"ic-field__label\" for=\"icGoalFrom\">Start from one of my Savings Goals</label>",
            page, StringComparison.Ordinal);
        string fund = Tag(page, "data-goal-id=\"1\"");
        Assert.Contains("data-target=\"25000\"", fund, StringComparison.Ordinal);
        Assert.Contains("data-years=\"3\"", fund, StringComparison.Ordinal); // 2.6 years, rounded up
        Assert.Contains("Emergency fund: $25,000", page, StringComparison.Ordinal);
        string car = Tag(page, "data-goal-id=\"2\"");
        Assert.Contains("data-target=\"18000\"", car, StringComparison.Ordinal);
        Assert.Contains("data-years=\"\"", car, StringComparison.Ordinal); // no target date: years stay as they are
        Assert.Contains("data-years=\"50\"", Tag(page, "data-goal-id=\"4\""), StringComparison.Ordinal);
        Assert.Contains("data-years=\"1\"", Tag(page, "data-goal-id=\"5\""), StringComparison.Ordinal);
        Assert.DoesNotContain("data-goal-id=\"3\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"icGoalFromUnavailable\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingsGoalPicker_ConvertsAGoalInAnotherCurrency_AndLeavesOutOneWithNoRate()
    {
        SavingsGoalDto house = Goal(6, "House deposit", 270_000_000m, targetDate: null);
        house.CurrencyCode = "VND";
        house.ReportingTargetAmount = 10_850.40m;
        SavingsGoalDto trip = Goal(7, "Hanoi trip", 30_000_000m, targetDate: null);
        trip.CurrencyCode = "VND";
        trip.ReportingTargetAmount = null;
        _goals.Goals.AddRange([house, trip]);

        string page = Calculator(await GetPageAsync());

        Assert.Contains("data-target=\"10851\"", Tag(page, "data-goal-id=\"6\""), StringComparison.Ordinal);
        Assert.Contains("House deposit: about $10,851, converted from VND", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-goal-id=\"7\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingsGoalPicker_IsLeftOut_WhenTheAccountHasNoActiveGoals()
    {
        _goals.Goals.Add(Goal(3, "Paid off", 5_000m, targetDate: null, completed: true));

        string page = Calculator(await GetPageAsync());

        Assert.DoesNotContain("id=\"icGoalFrom\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"icGoalFromUnavailable\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"icGoal\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingsGoalPicker_WhenTheApiIsUnavailable_SaysSoAndTheCalculatorStillRenders()
    {
        _goals.Unavailable = true;

        string page = Calculator(await GetPageAsync());

        Assert.DoesNotContain("id=\"icGoalFrom\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"icGoalFromUnavailable\"", page, StringComparison.Ordinal);
        Assert.Contains("Your Savings Goals could not be loaded", page, StringComparison.Ordinal);
        Assert.Contains("id=\"icGoal\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"icMonthlyRange\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingsGoalPicker_OnlyReadsTheGoals()
    {
        _goals.Goals.Add(Goal(1, "Emergency fund", 25_000m, targetDate: null));

        await GetPageAsync();

        Assert.Equal(["GetSavingsGoalsAsync"], _goals.Calls);
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

    private static SavingsGoalDto Goal(int id, string name, decimal target, DateTime? targetDate, bool completed = false) =>
        new()
        {
            Id = id,
            Name = name,
            CurrencyCode = "USD",
            ReportingCurrency = "USD",
            TargetAmount = target,
            ReportingTargetAmount = target,
            TargetDate = targetDate,
            IsCompleted = completed
        };

    /// <summary>The Savings Goals API: records every call, and every write fails the test.</summary>
    private sealed class GoalsApi : ISavingsGoalsApi
    {
        public List<SavingsGoalDto> Goals { get; } = [];

        public List<string> Calls { get; } = [];

        public bool Unavailable { get; set; }

        public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add(nameof(GetSavingsGoalsAsync));
            return Unavailable
                ? throw new HttpRequestException("The ApiService is down.")
                : Task.FromResult(Goals);
        }

        public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => Write<CreateSavingsGoalResponseDto>();

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => Write<UpdateSavingsGoalResponseDto>();

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
            CancellationToken cancellationToken = default) => Write<DeleteSavingsGoalResponseDto>();

        public Task<List<ContributionDto>> GetContributionsAsync(int goalId,
            CancellationToken cancellationToken = default) => Write<List<ContributionDto>>();

        public Task<SaveContributionResponseDto> AddContributionAsync(int goalId, SaveContributionRequestDto request,
            CancellationToken cancellationToken = default) => Write<SaveContributionResponseDto>();

        public Task<SaveContributionResponseDto> UpdateContributionAsync(int goalId, int contributionId,
            SaveContributionRequestDto request, CancellationToken cancellationToken = default) =>
            Write<SaveContributionResponseDto>();

        public Task<DeleteContributionResponseDto> DeleteContributionAsync(int goalId, int contributionId,
            CancellationToken cancellationToken = default) => Write<DeleteContributionResponseDto>();

        public Task<SaveContributionResponseDto> CorrectBalanceAsync(int goalId, CorrectBalanceRequestDto request,
            CancellationToken cancellationToken = default) => Write<SaveContributionResponseDto>();

        private Task<T> Write<T>([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            Calls.Add(caller);
            throw new NotSupportedException($"The calculator called {caller}.");
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
