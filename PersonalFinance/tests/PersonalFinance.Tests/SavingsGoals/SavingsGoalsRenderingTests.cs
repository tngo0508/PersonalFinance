using System.Globalization;
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
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The Savings Goals page rendered through the real compiled Razor views. A goal with nothing saved yet still shows its
/// percentage: the label sits beside the progress track, not inside a fill too narrow to hold it. Each card records
/// Contributions through an "Add Contribution" form, lists them in a collapsible history, and offers "Correct balance".
/// </summary>
public sealed partial class SavingsGoalsRenderingTests : IAsyncLifetime
{
    private const string SignedInHeader = "X-Test-Signed-In";
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly GoalsApi _api = new();

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
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddSingleton<IFeedbackApi, NoFeedbackApi>();
        builder.Services.AddSingleton<ISavingsGoalsApi>(_api);
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
            .AddApplicationPart(typeof(SavingsGoalsController).Assembly);

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
    public async Task GoalWithNothingSaved_ShowsItsPercentage_BesideAnEmptyFill()
    {
        _api.Goals.Add(EmptyGoal());

        (HttpStatusCode status, string html) = await GetPageAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Matches(EmptyFill(), html);
        Assert.Matches(LabelAfterTrack(), html);
        Assert.Contains("role=\"progressbar\"", ProgressTrack().Match(html).Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoalWithNothingSaved_InvitesItsFirstContribution()
    {
        _api.Goals.Add(EmptyGoal());

        (_, string html) = await GetPageAsync();

        Assert.Contains("Nothing saved yet. Add your first Contribution below", html, StringComparison.Ordinal);
        Assert.DoesNotContain("savings-goal-history", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Card_OffersTheAddContributionForm_InsteadOfTheUpdateInput()
    {
        _api.Goals.Add(EmptyGoal());

        (_, string html) = await GetPageAsync();

        string form = AddContributionForm().Match(html).Value;
        Assert.Contains("name=\"__RequestVerificationToken\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"goalId\" value=\"1\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", form, StringComparison.Ordinal);
        Assert.Matches("name=\"direction\" value=\"deposit\"[^>]*checked", form);
        Assert.Contains("name=\"direction\" value=\"withdrawal\"", form, StringComparison.Ordinal);
        // An optional date, never after today, and an optional short note.
        Assert.Matches("name=\"date\"[^>]*min=\"2000-01-01\" max=\"2026-10-25\"", form);
        Assert.Matches("name=\"note\"[^>]*maxlength=\"200\"", form);
        Assert.DoesNotContain("/SavingsGoals/Update", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"currentAmount\"", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_ListsContributionsNewestFirst_AndAnAdjustmentCanOnlyBeDeleted()
    {
        _api.Goals.Add(new SavingsGoalDto
        {
            Id = 4,
            Name = "Vacation",
            TargetAmount = 2_000m,
            CurrentAmount = 650m,
            ProgressPercent = 32.5,
            Contributions =
            [
                new ContributionDto { Id = 9, Amount = 50m, Date = new DateOnly(2026, 10, 20), Kind = ContributionKind.Adjustment },
                new ContributionDto { Id = 8, Amount = -100m, Date = new DateOnly(2026, 10, 12), Note = "Flights", Kind = ContributionKind.Regular },
                new ContributionDto { Id = 7, Amount = 700m, Date = new DateOnly(2026, 9, 1), Kind = ContributionKind.Opening }
            ]
        });

        (_, string html) = await GetPageAsync();

        Assert.Contains("<summary>History (3)</summary>", html, StringComparison.Ordinal);
        Assert.Equal(["9", "8", "7"], HistoryItem().Matches(html).Select(m => m.Groups[1].Value));
        Assert.Contains("Balance correction", html, StringComparison.Ordinal);
        Assert.Contains("Opening amount", html, StringComparison.Ordinal);
        Assert.Contains("Flights", html, StringComparison.Ordinal);
        Assert.Contains("10/12/2026", html, StringComparison.Ordinal);
        Assert.Contains(MoneyFormatter.Format(100m, SupportedCurrencies.Default.Code), html, StringComparison.Ordinal);
        // Every Contribution can be deleted; only the Regular and Opening ones can be edited.
        Assert.Equal(3, Regex.Count(html, "action=\"/SavingsGoals/DeleteContribution\""));
        Assert.Equal(2, Regex.Count(html, "action=\"/SavingsGoals/EditContribution\""));
        Assert.DoesNotContain("name=\"contributionId\" value=\"9\"/>\n", EditForms(html), StringComparison.Ordinal);
        // The edit form opens on the Contribution's own direction.
        Assert.Matches("id=\"withdrawal-4-8\" checked", html);
        Assert.DoesNotContain("Nothing saved yet", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Card_OffersCorrectBalance_StartingFromTheSavedAmount()
    {
        _api.Goals.Add(new SavingsGoalDto { Id = 4, Name = "Vacation", TargetAmount = 2_000m, CurrentAmount = 650m });

        (_, string html) = await GetPageAsync();

        string form = CorrectBalanceForm().Match(html).Value;
        Assert.Contains("name=\"__RequestVerificationToken\"", form, StringComparison.Ordinal);
        Assert.Contains($"value=\"{MoneyFormatter.FormatAmount(650m, SupportedCurrencies.Default.Code)}\"", form,
            StringComparison.Ordinal);
        Assert.Contains("<summary>Correct balance</summary>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Card_InVietnamese_ShowsTheContributionStringsAndDatesInVietnamese()
    {
        _api.Goals.Add(new SavingsGoalDto
        {
            Id = 4,
            Name = "Vacation",
            TargetAmount = 2_000m,
            CurrentAmount = 700m,
            Contributions =
                [new ContributionDto { Id = 7, Amount = 700m, Date = new DateOnly(2026, 9, 1), Kind = ContributionKind.Opening }]
        });

        (_, string html) = await GetPageAsync(VietnameseCookie);

        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Contribution.Deposit"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Contribution.Withdrawal"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Balance.Summary"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.History.Opening"), html, StringComparison.Ordinal);
        Assert.Contains(
            TestLocalization.Vietnamese("SavingsGoals.History.Summary").Replace("{count}", "1", StringComparison.Ordinal),
            html, StringComparison.Ordinal);
        Assert.Contains(new DateTime(2026, 9, 1).ToString("d", CultureInfo.GetCultureInfo("vi-VN")), html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(">Correct balance<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Deposit<", html, StringComparison.Ordinal);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetPageAsync(string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SavingsGoals");
        request.Headers.Add(SignedInHeader, "1");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        HttpResponseMessage response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string EditForms(string html) =>
        string.Concat(Regex.Matches(html, """<form [^>]*action="/SavingsGoals/EditContribution"[\s\S]*?</form>""")
            .Select(m => m.Value));

    private static SavingsGoalDto EmptyGoal() =>
        new() { Id = 1, Name = "Emergency Fund", TargetAmount = 27000m, CurrentAmount = 0m, ProgressPercent = 0 };

    [GeneratedRegex("""<div class="savings-goal-progress-fill[^"]*"\s+style="width:0%"></div>""")]
    private static partial Regex EmptyFill();

    [GeneratedRegex("""</div>\s*</div>\s*<span class="savings-goal-progress-label"[^>]*>0%</span>""")]
    private static partial Regex LabelAfterTrack();

    [GeneratedRegex("""<div class="savings-goal-progress-track"[^>]*>""")]
    private static partial Regex ProgressTrack();

    [GeneratedRegex("""<form [^>]*action="/SavingsGoals/AddContribution"[\s\S]*?</form>""")]
    private static partial Regex AddContributionForm();

    [GeneratedRegex("""<form [^>]*action="/SavingsGoals/CorrectBalance"[\s\S]*?</form>""")]
    private static partial Regex CorrectBalanceForm();

    [GeneratedRegex("""<li class="savings-history-item" data-contribution-id="(\d+)">""")]
    private static partial Regex HistoryItem();

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
                new Claim(ClaimTypes.NameIdentifier, "goals-user"),
                new Claim(ClaimTypes.Email, "goals@example.com"),
                new Claim(ClaimTypes.GivenName, "Linh")
            ], IdentityConstants.ApplicationScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ApplicationScheme)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Returns the goals a test sets up; the page only reads them.</summary>
    private sealed class GoalsApi : ISavingsGoalsApi
    {
        public List<SavingsGoalDto> Goals { get; } = [];

        public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Goals);

        public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<List<ContributionDto>> GetContributionsAsync(int goalId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SaveContributionResponseDto> AddContributionAsync(int goalId, SaveContributionRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SaveContributionResponseDto> UpdateContributionAsync(int goalId, int contributionId,
            SaveContributionRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DeleteContributionResponseDto> DeleteContributionAsync(int goalId, int contributionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SaveContributionResponseDto> CorrectBalanceAsync(int goalId, CorrectBalanceRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
