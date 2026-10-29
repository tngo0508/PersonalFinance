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
/// Contributions through an "Add Contribution" form, lists them in a collapsible history, offers "Correct balance", and
/// says its Pace in words; a four-tile summary sits above the goals.
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
        builder.Services.AddSingleton<PersonalFinance.Web.Services.ILearningLibraryStatus>(new PersonalFinance.Tests.TestSupport.FixedLearningLibraryStatus(false));
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

    [Theory]
    [InlineData(PaceStatus.NoTargetDate, "No target date", "This goal has no target date to compare with.")]
    [InlineData(PaceStatus.JustStarted, "Just started", "This goal has just started, so it&#x27;s too early to compare with its date.")]
    [InlineData(PaceStatus.Ahead, "Ahead", "You&#x27;re ahead of the line to your date.")]
    [InlineData(PaceStatus.OnTrack, "On track", "You&#x27;re on track for your date.")]
    [InlineData(PaceStatus.Behind, "Behind", "You&#x27;re behind the line to your date.")]
    [InlineData(PaceStatus.Reached, "Reached", "This goal has reached its target.")]
    public async Task PaceLine_SaysTheStatusInWords(PaceStatus status, string label, string sentence)
    {
        _api.Goals.Add(PacedGoal(new SavingsGoalPaceDto { Status = status }));

        (_, string html) = await GetPageAsync();

        string pace = PaceBlock().Match(html).Value;
        Assert.Contains($"data-pace-status=\"{status}\"", pace, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"savings-goal-pace__label\">{label}</span>", pace, StringComparison.Ordinal);
        Assert.Contains(sentence, pace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaceLine_ForAPastDueGoal_SaysWhatIsLeft()
    {
        _api.Goals.Add(PacedGoal(new SavingsGoalPaceDto { Status = PaceStatus.PastDue }));

        (_, string html) = await GetPageAsync();

        Assert.Contains($"The target date has passed, with {MoneyFormatter.FormatWhole(1_350m, SupportedCurrencies.Default.Code)} still to go.",
            PaceBlock().Match(html).Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaceLine_ShowsNeededPerMonth_TheProjection_AndSavedThisMonth()
    {
        SavingsGoalDto goal = PacedGoal(new SavingsGoalPaceDto
        {
            Status = PaceStatus.Behind,
            NeededPerMonth = 112.5m,
            RateState = PaceRateState.Positive,
            RatePerMonth = 80m,
            ProjectedFinish = new DateOnly(2028, 3, 14)
        });
        goal.TargetDate = new DateTime(2027, 10, 1);
        goal.SavedThisMonth = 40m;
        _api.Goals.Add(goal);

        (_, string html) = await GetPageAsync();

        string pace = PaceBlock().Match(html).Value;
        Assert.Contains("$112.50 a month would reach the target by October 2027.", pace, StringComparison.Ordinal);
        Assert.Contains("At your recent pace you&#x27;d reach this in March 2028.", pace, StringComparison.Ordinal);
        Assert.Contains("Saved this month: $40.00", pace, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PaceRateState.Unknown, "Not enough history yet for a projection.")]
    [InlineData(PaceRateState.Stalled, "No recent Contributions to project from.")]
    public async Task PaceLine_WithoutAProjection_SaysWhy(PaceRateState state, string sentence)
    {
        _api.Goals.Add(PacedGoal(new SavingsGoalPaceDto { Status = PaceStatus.NoTargetDate, RateState = state }));

        (_, string html) = await GetPageAsync();

        Assert.Contains(sentence, PaceBlock().Match(html).Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaceLine_ReadsAsInformation_NeverAdvice()
    {
        _api.Goals.Add(PacedGoal(new SavingsGoalPaceDto { Status = PaceStatus.Behind, NeededPerMonth = 50m }));

        (_, string html) = await GetPageAsync();

        string pace = PaceBlock().Match(html).Value;
        Assert.DoesNotContain("should", pace, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("need to", pace, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummaryHeader_AddsUpTheGoals()
    {
        _api.Goals.AddRange(
        [
            Goal(1, "Car", 400m, 1_000m, new DateTime(2027, 6, 1), PaceStatus.OnTrack, savedThisMonth: 50m),
            Goal(2, "House", 2_000m, 20_000m, new DateTime(2026, 12, 1), PaceStatus.Behind, savedThisMonth: 25.5m),
            Goal(3, "Trip", 500m, 2_000m, new DateTime(2026, 9, 1), PaceStatus.PastDue),
            Goal(4, "Laptop", 1_500m, 1_500m, new DateTime(2026, 11, 1), PaceStatus.Reached, completed: true),
            Goal(5, "Rainy day", 300m, 5_000m, null, PaceStatus.NoTargetDate, savedThisMonth: 10m)
        ]);

        (_, string html) = await GetPageAsync();

        // Active goals only: 400 + 2,000 + 500 + 300; Laptop is reached.
        Assert.Contains($"data-summary=\"total-saved\">{MoneyFormatter.FormatWhole(3_200m, SupportedCurrencies.Default.Code)}<",
            html, StringComparison.Ordinal);
        Assert.Contains($"data-summary=\"saved-this-month\">{MoneyFormatter.FormatWhole(85.5m, SupportedCurrencies.Default.Code)}<",
            html, StringComparison.Ordinal);
        // Dated goals with a verdict: Car, House, Trip (Past due); Laptop is reached, Rainy day undated.
        Assert.Contains("data-summary=\"on-track\">1 of 3<", html, StringComparison.Ordinal);
        // The nearest target date still ahead, of a goal not reached: House, not Trip (past) or Laptop (reached).
        Assert.Matches("data-summary=\"next-target\">12/1/2026</span>\\s*<span class=\"savings-summary__note\">House</span>", html);
    }

    [Fact]
    public async Task SummaryHeader_WithoutDatedGoals_SaysSo()
    {
        _api.Goals.Add(Goal(5, "Rainy day", 300m, 5_000m, null, PaceStatus.NoTargetDate));

        (_, string html) = await GetPageAsync();

        Assert.Contains("No dated goals to compare yet", html, StringComparison.Ordinal);
        Assert.Contains("No upcoming target date", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoGoals_ShowsNoSummaryHeader()
    {
        (_, string html) = await GetPageAsync();

        Assert.DoesNotContain("class=\"savings-summary\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PaceAndSummary_InVietnamese_ShowNoEnglish()
    {
        SavingsGoalDto goal = PacedGoal(new SavingsGoalPaceDto
        {
            Status = PaceStatus.OnTrack,
            NeededPerMonth = 100m,
            RateState = PaceRateState.Positive,
            RatePerMonth = 120m,
            ProjectedFinish = new DateOnly(2027, 8, 1)
        });
        goal.TargetDate = new DateTime(2027, 10, 1);
        _api.Goals.Add(goal);
        CultureInfo vietnam = CultureInfo.GetCultureInfo("vi-VN");

        (_, string html) = await GetPageAsync(VietnameseCookie);

        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Pace.Status.OnTrack"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Summary.SavedThisMonth"), html, StringComparison.Ordinal);
        Assert.Contains(TestLocalization.Vietnamese("SavingsGoals.Pace.Projection")
            .Replace("{month}", new DateOnly(2027, 8, 1).ToString("Y", vietnam), StringComparison.Ordinal), html, StringComparison.Ordinal);
        Assert.DoesNotContain("On track", html, StringComparison.Ordinal);
        Assert.DoesNotContain("At your recent pace", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved this month", html, StringComparison.Ordinal);
    }

    private static SavingsGoalDto PacedGoal(SavingsGoalPaceDto pace) =>
        new() { Id = 4, Name = "Vacation", TargetAmount = 2_000m, CurrentAmount = 650m, Pace = pace };

    private static SavingsGoalDto Goal(int id, string name, decimal saved, decimal target, DateTime? targetDate,
        PaceStatus status, decimal savedThisMonth = 0m, bool completed = false) =>
        new()
        {
            Id = id,
            Name = name,
            CurrentAmount = saved,
            TargetAmount = target,
            TargetDate = targetDate,
            IsCompleted = completed,
            SavedThisMonth = savedThisMonth,
            Pace = new SavingsGoalPaceDto { Status = status }
        };

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

    [GeneratedRegex("""<div class="savings-goal-pace[\s\S]*?savings-goal-pace__month[^<]*</p>""")]
    private static partial Regex PaceBlock();

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
