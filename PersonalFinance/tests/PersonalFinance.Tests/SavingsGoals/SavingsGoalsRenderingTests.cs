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

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// The Savings Goals page rendered through the real compiled Razor views. A goal with nothing saved yet still shows its
/// percentage: the label sits beside the progress track, not inside a fill too narrow to hold it.
/// </summary>
public sealed partial class SavingsGoalsRenderingTests : IAsyncLifetime
{
    private const string SignedInHeader = "X-Test-Signed-In";

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
        builder.Services.AddSingleton<ISavingsGoalsApi, OneEmptyGoalApi>();
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SavingsGoals");
        request.Headers.Add(SignedInHeader, "1");

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(EmptyFill(), html);
        Assert.Matches(LabelAfterTrack(), html);
        Assert.Contains("role=\"progressbar\"", ProgressTrack().Match(html).Value, StringComparison.Ordinal);
    }

    [GeneratedRegex("""<div class="savings-goal-progress-fill[^"]*"\s+style="width:0%"></div>""")]
    private static partial Regex EmptyFill();

    [GeneratedRegex("""</div>\s*</div>\s*<span class="savings-goal-progress-label"[^>]*>0%</span>""")]
    private static partial Regex LabelAfterTrack();

    [GeneratedRegex("""<div class="savings-goal-progress-track"[^>]*>""")]
    private static partial Regex ProgressTrack();

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

    private sealed class OneEmptyGoalApi : ISavingsGoalsApi
    {
        public Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SavingsGoalDto>
            {
                new() { Id = 1, Name = "Emergency Fund", TargetAmount = 27000m, CurrentAmount = 0m, ProgressPercent = 0 }
            });

        public Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync(CreateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, UpdateSavingsGoalRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id,
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
