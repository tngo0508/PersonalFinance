using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Areas.Identity.Pages.Account;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The scaffolded Identity pages (slice L3) render in the visitor's language, through the real views and layout, and
/// every key they name exists: no page logs event 3402 <c>LocalizationNotFound</c>.
/// </summary>
public sealed class IdentityPagesLocalizationTests : IAsyncLifetime
{
    private const string VietnameseCookie = ".AspNetCore.Culture=c%3Dvi-VN%7Cuic%3Dvi";
    private const string UserId = "user-1";

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _notFound = new();
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
        builder.Logging.ClearProviders().AddProvider(new LogCapture(_notFound));
        string database = $"IdentityPagesLocalization_{Guid.NewGuid():N}";
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddDefaultIdentity<IdentityUser>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();
        // Signed in as UserId whenever the request says so, without a sign-in round trip.
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IApiServiceWarmer, NoOpWarmer>();
        builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
        builder.Services.AddSingleton<IUserPreferenceService, InMemoryUserPreferenceService>();
        builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
        builder.Services.AddScoped<IUserConsentService, UserConsentService>();
        // The pages only call ApiService when a form is posted, never on GET.
        builder.Services.AddSingleton(NotCalled<IAccountDeletionApi>());
        builder.Services.AddSingleton(NotCalled<IDataExportApi>());
        builder.Services.AddAppLocalization(builder.Environment);
        // The Web project's parts go first, as they do in the app (its entry assembly), so its scaffolded pages win
        // over the Identity UI library's copies.
        builder.Services.AddRazorPages().ConfigureApplicationPartManager(manager =>
        {
            Assembly web = typeof(LoginModel).Assembly;
            List<ApplicationPart> parts = ApplicationPartFactory.GetApplicationPartFactory(web).GetApplicationParts(web).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                manager.ApplicationParts.Insert(i, parts[i]);
            }
        });
        builder.Services.AddSingleton<PersonalFinance.Web.Services.ILearningLibraryStatus>(new PersonalFinance.Tests.TestSupport.FixedLearningLibraryStatus(false));
        builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddViewLocalization()
            .AddDataAnnotationsLocalization();

        _app = builder.Build();
        _app.UseRequestLocalization();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAntiforgery();
        _app.MapRazorPages();
        _app.MapControllerRoute("default", "{controller=Home}/{action=Index}");
        await _app.StartAsync();

        using (IServiceScope scope = _app.Services.CreateScope())
        {
            UserManager<IdentityUser> users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            IdentityResult created = await users.CreateAsync(
                new IdentityUser { Id = UserId, UserName = "someone@example.com", Email = "someone@example.com" },
                "Passw0rd!x");
            Assert.True(created.Succeeded);
        }

        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task SignIn_InVietnamese_ShowsVietnameseText()
    {
        string html = await GetAsync("/Identity/Account/Login", signedIn: false);

        Assert.Contains("<html lang=\"vi\"", html);
        Assert.Contains("<h1 class=\"auth-page__title mb-2\">Đăng nhập</h1>", html);
        Assert.Contains("Chưa có tài khoản?", html);
        Assert.DoesNotContain("have an account?", html);
        Assert.Empty(_notFound);
    }

    [Fact]
    public async Task ChangePassword_InVietnamese_ShowsVietnameseFormAndSettingsNav()
    {
        string html = await GetAsync("/Identity/Account/Manage/ChangePassword", signedIn: true);

        Assert.Contains("<h3>Thay đổi mật khẩu</h3>", html);
        Assert.Contains(">Xác thực hai yếu tố</a>", html);
        Assert.Contains(">Mật khẩu hiện tại</label>", html);
        Assert.DoesNotContain("Update password", html);
        Assert.DoesNotContain("Account settings", html);
        Assert.Empty(_notFound);
    }

    [Theory]
    [InlineData("/Identity/Account/Register", false)]
    [InlineData("/Identity/Account/ForgotPassword", false)]
    [InlineData("/Identity/Account/ForgotPasswordConfirmation", false)]
    [InlineData("/Identity/Account/ResendEmailConfirmation", false)]
    [InlineData("/Identity/Account/ResetPassword?code=AAAA", false)]
    [InlineData("/Identity/Account/ResetPasswordConfirmation", false)]
    [InlineData("/Identity/Account/Lockout", false)]
    [InlineData("/Identity/Account/AccessDenied", false)]
    [InlineData("/Identity/Account/Logout", false)]
    [InlineData("/Identity/Account/DeletionRequested?purgeAfter=2026-11-24&emailSent=false", false)]
    [InlineData("/Identity/Account/Manage", true)]
    [InlineData("/Identity/Account/Manage/Email", true)]
    [InlineData("/Identity/Account/Manage/TwoFactorAuthentication", true)]
    [InlineData("/Identity/Account/Manage/EnableAuthenticator", true)]
    [InlineData("/Identity/Account/Manage/ExternalLogins", true)]
    [InlineData("/Identity/Account/Manage/ResetAuthenticator", true)]
    [InlineData("/Identity/Account/Manage/PersonalData", true)]
    [InlineData("/Identity/Account/Manage/DeletePersonalData", true)]
    [InlineData("/Identity/Account/CancelDeletion?token=cancel-token", false)]
    [InlineData("/Identity/Account/RegisterConfirmation?email=someone@example.com", false)]
    public async Task AnyIdentityPage_InVietnamese_RendersWithoutAMissingKey(string url, bool signedIn)
    {
        string html = await GetAsync(url, signedIn);

        Assert.Contains("<html lang=\"vi\"", html);
        Assert.Empty(_notFound);
    }

    [Fact]
    public async Task ConfirmEmail_WithABadCode_ShowsTheVietnameseErrorAsAnError()
    {
        string html = await GetAsync($"/Identity/Account/ConfirmEmail?userId={UserId}&code=AAAA", signedIn: false);

        Assert.Contains("alert-danger", html);
        Assert.Contains("Lỗi khi xác nhận email của bạn.", html);
        Assert.DoesNotContain("Error confirming your email.", html);
        Assert.Empty(_notFound);
    }

    [Fact]
    public async Task SignIn_InEnglish_IsUnchanged()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Identity/Account/Login");
        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<html lang=\"en\"", html);
        Assert.Contains("See your plan and your progress to your FI target.", html);
        Assert.Contains("have an account?", html);
        Assert.Empty(_notFound);
    }

    private async Task<string> GetAsync(string url, bool signedIn)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", VietnameseCookie);
        if (signedIn)
        {
            request.Headers.Add(TestAuthHandler.Header, UserId);
        }

        HttpResponseMessage response = await _client.SendAsync(request);
        string html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {html}");
        return html;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out Microsoft.Extensions.Primitives.StringValues userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private static T NotCalled<T>() where T : class => DispatchProxy.Create<T, NotCalledProxy>();

    /// <summary>An ApiService client the page must not call while rendering.</summary>
    public class NotCalledProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"{targetMethod?.Name} was called while rendering the page.");
    }

    private sealed class NoOpWarmer : IApiServiceWarmer
    {
        public Task WarmUpAsync() => Task.CompletedTask;
    }

    /// <summary>Keeps every 3402 <c>LocalizationNotFound</c> entry, from any logger.</summary>
    private sealed class LogCapture(System.Collections.Concurrent.ConcurrentQueue<string> notFound) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(notFound);

        public void Dispose()
        {
        }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> notFound) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (eventId.Id == 3402)
                {
                    notFound.Enqueue(formatter(state, exception));
                }
            }
        }
    }
}
