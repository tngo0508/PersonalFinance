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
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// The Google Drive page rendered through the real compiled Razor views when a connected drive's files cannot be loaded
/// (#476). The empty state explains the failure once, with the connection's stored error when it has one, and offers
/// two actions: Try again and Update API key. No page-level banner repeats the cause.
/// </summary>
public sealed partial class GoogleDriveErrorStateRenderingTests : IAsyncLifetime
{
    private const string SignedInHeader = "X-Test-Signed-In";
    private const string StoredError = "Google credentials are invalid or expired. Please re-authenticate or update your API key.";
    private const string SharingText = "This usually means the folder sharing settings changed. Try these steps:";

    private readonly FakeGoogleDriveApi _api = new();

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
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddSingleton<IFeedbackApi, NoFeedbackApi>();
        builder.Services.AddSingleton<IGoogleDriveApi>(_api);
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
            .AddApplicationPart(typeof(GoogleDriveController).Assembly);

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
    public async Task InvalidConnection_RendersTheErrorState_WithItsStoredErrorOnce()
    {
        UseInvalidConnection(StoredError);

        string html = await GetPageAsync();

        Assert.Contains("We couldn&#x27;t load the files in Family Budget", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, Regex.Escape(StoredError)));
        Assert.DoesNotContain(SharingText, html, StringComparison.Ordinal);
        Assert.DoesNotContain("gd-alert--danger", html, StringComparison.Ordinal);
        // The loaded view's needs-reauth notice is gone with the branch that rendered it.
        Assert.DoesNotContain("This drive needs a new API key.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidConnection_WithoutAStoredError_FallsBackToTheSharingSteps()
    {
        UseInvalidConnection(storedError: null);

        string html = await GetPageAsync();

        Assert.Contains(SharingText, html, StringComparison.Ordinal);
        Assert.Contains("Check that the folder still exists", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidConnection_OffersTryAgainAndUpdateApiKey_ButNoSecondHelp()
    {
        UseInvalidConnection(StoredError);

        string html = await GetPageAsync();

        string actions = EmptyStateActions().Match(html).Value;
        Assert.Contains("Try again", actions, StringComparison.Ordinal);
        Assert.Contains("btn btn-primary", actions, StringComparison.Ordinal);
        Assert.Contains("Update API key", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("#apiKeyGuideModal", actions, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(actions, "<button\\b"));
    }

    private void UseInvalidConnection(string? storedError)
    {
        _api.SavedConnections =
        [
            new GoogleDriveConnectionDto
            {
                Id = 7, Name = "Family Budget", FolderId = "folder-7", IsValid = false,
                SyncStatus = storedError is null ? "Failed" : "NeedsReauth", ErrorMessage = storedError
            }
        ];
        _api.ConnectionFilesResponse = new GoogleDriveFolderResponseDto
        {
            Success = false, FolderId = "folder-7", ErrorMessage = storedError
        };
    }

    private async Task<string> GetPageAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/GoogleDrive");
        request.Headers.Add(SignedInHeader, "1");

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"/GoogleDrive returned {response.StatusCode}: {html}");
        return html.ReplaceLineEndings("\n");
    }

    [GeneratedRegex("<div class=\"gd-empty-state__actions\">.*?</div>\\s*</div>", RegexOptions.Singleline)]
    private static partial Regex EmptyStateActions();

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
                new Claim(ClaimTypes.NameIdentifier, "drive-user"),
                new Claim(ClaimTypes.Email, "drive@example.com")
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
