using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Areas.Identity.Pages.Account;

namespace PersonalFinance.Tests.Security;

/// <summary>
/// Google sign-in. The callback tests drive the real <see cref="ExternalLoginModel.OnGetCallbackAsync"/>; until #350
/// they re-did its provisioning, linking and claim sync through <see cref="UserManager{TUser}"/> themselves, so the page
/// could change without a test noticing. The cookie redirects belong to <c>ApiAwareCookieRedirectsTests</c>.
/// </summary>
public class ExternalAuthenticationTests
{
    private const string OldPicture = "https://old.url/photo.jpg";
    private const string NewPicture = "https://new.url/photo.jpg";

    [Fact]
    public async Task GoogleCallback_ForANewEmail_CreatesAConfirmedAccount_LinksGoogle_AndSignsIn()
    {
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options { AccountExists = false });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
        IdentityUser user = Assert.IsType<IdentityUser>(await harness.UserManager.FindByEmailAsync(ExternalLoginCallbackHarness.Email));
        Assert.True(user.EmailConfirmed);
        UserLoginInfo login = Assert.Single(await harness.UserManager.GetLoginsAsync(user));
        Assert.Equal(("Google", ExternalLoginCallbackHarness.ProviderKey), (login.LoginProvider, login.ProviderKey));
        Assert.True(await harness.UserManager.IsInRoleAsync(user, AppRoles.User));
        Assert.Equal(1, harness.SignInManager.SignInCalls);
    }

    [Fact]
    public async Task GoogleCallback_ForAConfirmedAccountWithoutGoogle_LinksGoogle_AndSignsIn()
    {
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options { EmailConfirmed = true, GoogleLinked = false });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
        UserLoginInfo login = Assert.Single(await harness.UserManager.GetLoginsAsync(harness.User!));
        Assert.Equal(("Google", ExternalLoginCallbackHarness.ProviderKey), (login.LoginProvider, login.ProviderKey));
        Assert.Equal(1, harness.SignInManager.SignInCalls);
    }

    [Fact]
    public async Task GoogleCallback_ForAnUnconfirmedAccount_LinksNothing_AndAsksForTheEmailToBeConfirmed()
    {
        using var uiCulture = new UiCultureScope("en");
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options { EmailConfirmed = false });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(await harness.UserManager.GetLoginsAsync(harness.User!));
        Assert.Equal(0, harness.SignInManager.SignInCalls);
        Assert.Equal(
            "An account with this email address already exists but has not been confirmed. Please confirm your email address first.",
            Assert.Single(harness.Page.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task GoogleCallback_ForALinkedAccount_ReplacesItsStoredPicture_WithGooglesCurrentOne()
    {
        await using ExternalLoginCallbackHarness harness = await ExternalLoginCallbackHarness.CreateAsync(
            new ExternalLoginCallbackHarness.Options
            {
                GoogleLinked = true,
                StoredClaims = [new Claim("urn:google:picture", OldPicture)],
                GoogleClaims = [new Claim("urn:google:picture", NewPicture)]
            });

        IActionResult result = await harness.Page.OnGetCallbackAsync();

        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
        IList<Claim> claims = await harness.UserManager.GetClaimsAsync(harness.User!);
        Assert.Equal(NewPicture, Assert.Single(claims, c => c.Type == "urn:google:picture").Value);
    }

    [Fact]
    public void AGoogleLogin_IsKeyedByProviderAndProviderKey_SoItCannotBelongToTwoAccounts()
    {
        using ServiceProvider provider = BuildTestServiceProvider();
        AppDbContext db = provider.GetRequiredService<AppDbContext>();

        IEnumerable<string> key = db.Model.FindEntityType(typeof(IdentityUserLogin<string>))!
            .FindPrimaryKey()!.Properties.Select(p => p.Name);

        Assert.Equal(["LoginProvider", "ProviderKey"], key);
    }

    [Fact]
    public void ExternalLoginModel_OnGet_RedirectsToLoginPage()
    {
        using ServiceProvider provider = BuildTestServiceProvider();
        var model = new ExternalLoginModel(
            provider.GetRequiredService<SignInManager<IdentityUser>>(),
            provider.GetRequiredService<UserManager<IdentityUser>>(),
            provider.GetRequiredService<IUserStore<IdentityUser>>(),
            NullLogger<ExternalLoginModel>.Instance,
            new RecordingAccountEmailService(),
            new InMemoryUserPreferenceService(),
            TestLocalization.Localizer);

        IActionResult result = model.OnGet();

        Assert.Equal("./Login", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    // The three Google registration tests below exercise a copy of the Google block in Web Program.cs, made inside
    // BuildTestServiceProvider, so they cannot fail when Program.cs changes. Proving the real registration needs it moved
    // into a helper Program.cs calls (as ApiAwareCookieRedirects was); that production change awaits approval (#350).

    [Fact]
    public async Task AuthenticationSchemeProvider_RegistersGoogleScheme_WhenConfigured()
    {
        using ServiceProvider provider = BuildTestServiceProvider(
            googleClientId: "test-google-client-id.apps.googleusercontent.com",
            googleClientSecret: "test-google-client-secret");

        IAuthenticationSchemeProvider schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        AuthenticationScheme? googleScheme = await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme);

        Assert.NotNull(googleScheme);
        Assert.Equal(GoogleDefaults.AuthenticationScheme, googleScheme.Name);
        Assert.Equal(GoogleDefaults.DisplayName, googleScheme.DisplayName);
    }

    [Fact]
    public void GoogleOptions_ConfiguresScopesAndClaimActions()
    {
        using ServiceProvider provider = BuildTestServiceProvider(
            googleClientId: "test-google-client-id.apps.googleusercontent.com",
            googleClientSecret: "test-google-client-secret");

        GoogleOptions options = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);

        Assert.Equal("test-google-client-id.apps.googleusercontent.com", options.ClientId);
        Assert.Equal("test-google-client-secret", options.ClientSecret);
        Assert.Equal("/signin-google", options.CallbackPath);
        Assert.Contains("profile", options.Scope);
        Assert.Contains("email", options.Scope);
    }

    [Fact]
    public async Task AuthenticationSchemeProvider_DoesNotRegisterGoogleScheme_WhenNotConfigured()
    {
        using ServiceProvider provider = BuildTestServiceProvider(googleClientId: null, googleClientSecret: null);

        IAuthenticationSchemeProvider schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Null(await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme));
    }

    private static ServiceProvider BuildTestServiceProvider(string? googleClientId = null, string? googleClientSecret = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"AuthTest_{Guid.NewGuid():N}"));
        services.AddDefaultIdentity<IdentityUser>().AddEntityFrameworkStores<AppDbContext>();

        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            services.AddAuthentication().AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = true;

                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
                options.ClaimActions.MapJsonKey("urn:google:locale", "locale", "string");
                options.ClaimActions.MapJsonKey("urn:google:verified_email", "email_verified", "bool");
            });
        }

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "PersonalFinance.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
    }
}
