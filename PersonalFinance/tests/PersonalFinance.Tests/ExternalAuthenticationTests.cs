using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using PersonalFinance.Web.Areas.Identity.Pages.Account;
using Xunit;

namespace PersonalFinance.Tests;

public class ExternalAuthenticationTests
{
    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "PersonalFinance.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
    }

    private static ServiceProvider BuildTestServiceProvider(
        bool requireConfirmedAccount = true,
        string? googleClientId = null,
        string? googleClientSecret = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
        services.AddDbContext<AppDbContext, SqliteAppDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        services.AddDefaultIdentity<IdentityUser>(options =>
            options.SignIn.RequireConfirmedAccount = requireConfirmedAccount)
            .AddEntityFrameworkStores<AppDbContext>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.LoginPath = "/Identity/Account/Login";
            options.LogoutPath = "/Identity/Account/Logout";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";

            // Prevent 302 redirects on API / Fetch / XMLHttpRequest endpoints; return 401/403 instead
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Headers.Accept.Any(h => h != null && h.Contains("application/json")) ||
                    context.Request.Headers.XRequestedWith == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Headers.Accept.Any(h => h != null && h.Contains("application/json")) ||
                    context.Request.Headers.XRequestedWith == "XMLHttpRequest")
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

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

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();

        return provider;
    }

    [Fact]
    public async Task AuthenticationSchemeProvider_RegistersGoogleScheme_WhenConfigured()
    {
        using var provider = BuildTestServiceProvider(
            googleClientId: "test-google-client-id.apps.googleusercontent.com",
            googleClientSecret: "test-google-client-secret");

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var googleScheme = await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme);

        Assert.NotNull(googleScheme);
        Assert.Equal(GoogleDefaults.AuthenticationScheme, googleScheme.Name);
        Assert.Equal(GoogleDefaults.DisplayName, googleScheme.DisplayName);
    }

    [Fact]
    public void GoogleOptions_ConfiguresScopesAndClaimActions()
    {
        using var provider = BuildTestServiceProvider(
            googleClientId: "test-google-client-id.apps.googleusercontent.com",
            googleClientSecret: "test-google-client-secret");

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>();
        var options = optionsMonitor.Get(GoogleDefaults.AuthenticationScheme);

        Assert.NotNull(options);
        Assert.Equal("test-google-client-id.apps.googleusercontent.com", options.ClientId);
        Assert.Equal("test-google-client-secret", options.ClientSecret);
        Assert.Equal("/signin-google", options.CallbackPath);
        Assert.Contains("profile", options.Scope);
        Assert.Contains("email", options.Scope);
    }

    [Fact]
    public async Task AuthenticationSchemeProvider_DoesNotRegisterGoogleScheme_WhenNotConfigured()
    {
        using var provider = BuildTestServiceProvider(
            googleClientId: null,
            googleClientSecret: null);

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var googleScheme = await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme);

        Assert.Null(googleScheme);
    }

    [Fact]
    public async Task ApplicationCookie_OnRedirectToLogin_Returns401_ForApiAndJsonRequests()
    {
        using var provider = BuildTestServiceProvider();
        var optionsSnapshot = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var cookieOptions = optionsSnapshot.Get(IdentityConstants.ApplicationScheme);

        // Scenario 1: API path segment
        var httpContextApi = new DefaultHttpContext();
        httpContextApi.Request.Path = "/api/items";
        var redirectContextApi = new RedirectContext<CookieAuthenticationOptions>(
            httpContextApi,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            "/Identity/Account/Login?ReturnUrl=%2Fapi%2Fitems");

        await cookieOptions.Events.OnRedirectToLogin(redirectContextApi);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContextApi.Response.StatusCode);

        // Scenario 2: JSON Accept header
        var httpContextJson = new DefaultHttpContext();
        httpContextJson.Request.Path = "/dashboard/data";
        httpContextJson.Request.Headers.Accept = "application/json";
        var redirectContextJson = new RedirectContext<CookieAuthenticationOptions>(
            httpContextJson,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            "/Identity/Account/Login?ReturnUrl=%2Fdashboard%2Fdata");

        await cookieOptions.Events.OnRedirectToLogin(redirectContextJson);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContextJson.Response.StatusCode);

        // Scenario 3: Standard browser request redirects (302)
        var httpContextBrowser = new DefaultHttpContext();
        httpContextBrowser.Request.Path = "/GoogleDrive";
        var redirectContextBrowser = new RedirectContext<CookieAuthenticationOptions>(
            httpContextBrowser,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            "/Identity/Account/Login?ReturnUrl=%2FGoogleDrive");

        await cookieOptions.Events.OnRedirectToLogin(redirectContextBrowser);
        Assert.Equal(StatusCodes.Status302Found, httpContextBrowser.Response.StatusCode);
        Assert.Equal("/Identity/Account/Login?ReturnUrl=%2FGoogleDrive", httpContextBrowser.Response.Headers.Location);
    }

    [Fact]
    public async Task ApplicationCookie_OnRedirectToAccessDenied_Returns403_ForApiAndJsonRequests()
    {
        using var provider = BuildTestServiceProvider();
        var optionsSnapshot = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var cookieOptions = optionsSnapshot.Get(IdentityConstants.ApplicationScheme);

        // Scenario 1: API path segment returns 403
        var httpContextApi = new DefaultHttpContext();
        httpContextApi.Request.Path = "/api/secure-endpoint";
        var redirectContextApi = new RedirectContext<CookieAuthenticationOptions>(
            httpContextApi,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            "/Identity/Account/AccessDenied");

        await cookieOptions.Events.OnRedirectToAccessDenied(redirectContextApi);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContextApi.Response.StatusCode);

        // Scenario 2: Standard browser request redirects (302)
        var httpContextBrowser = new DefaultHttpContext();
        httpContextBrowser.Request.Path = "/admin";
        var redirectContextBrowser = new RedirectContext<CookieAuthenticationOptions>(
            httpContextBrowser,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            "/Identity/Account/AccessDenied");

        await cookieOptions.Events.OnRedirectToAccessDenied(redirectContextBrowser);
        Assert.Equal(StatusCodes.Status302Found, httpContextBrowser.Response.StatusCode);
        Assert.Equal("/Identity/Account/AccessDenied", httpContextBrowser.Response.Headers.Location);
    }

    [Fact]
    public async Task ExternalLogin_AutoProvisionsNewUser_WithConfirmedEmailAndLoginInfo()
    {
        using var provider = BuildTestServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var email = "new_google_user@example.com";
        var loginInfo = new UserLoginInfo("Google", "google-sub-id-12345", "Google");

        // Simulate provisioning a new user from Google OAuth
        var newUser = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true // Verified OAuth email auto-confirmed
        };

        var createResult = await userManager.CreateAsync(newUser);
        Assert.True(createResult.Succeeded);

        var addLoginResult = await userManager.AddLoginAsync(newUser, loginInfo);
        Assert.True(addLoginResult.Succeeded);

        // Add Google profile claims (picture avatar, given name, locale)
        var claims = new[]
        {
            new Claim("urn:google:picture", "https://lh3.googleusercontent.com/a/avatar123"),
            new Claim(ClaimTypes.GivenName, "Jane"),
            new Claim("urn:google:locale", "en")
        };
        var addClaimsResult = await userManager.AddClaimsAsync(newUser, claims);
        Assert.True(addClaimsResult.Succeeded);

        // Verify lookup by login provider
        var userFromLogin = await userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey);
        Assert.NotNull(userFromLogin);
        Assert.Equal(email, userFromLogin.Email);
        Assert.True(userFromLogin.EmailConfirmed);

        // Verify claims persisted
        var userClaims = await userManager.GetClaimsAsync(userFromLogin);
        Assert.Contains(userClaims, c => c.Type == "urn:google:picture" && c.Value == "https://lh3.googleusercontent.com/a/avatar123");
        Assert.Contains(userClaims, c => c.Type == ClaimTypes.GivenName && c.Value == "Jane");

        // Verify that signInManager allows sign in
        var canSignIn = await signInManager.CanSignInAsync(userFromLogin);
        Assert.True(canSignIn);
    }

    [Fact]
    public async Task ExternalLogin_LinksToExistingConfirmedUser_Successfully()
    {
        using var provider = BuildTestServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();

        var email = "existing_user@example.com";
        var existingUser = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(existingUser, "Password123!");
        Assert.True(createResult.Succeeded);

        var loginInfo = new UserLoginInfo("Google", "google-sub-id-99999", "Google");
        var addLoginResult = await userManager.AddLoginAsync(existingUser, loginInfo);
        Assert.True(addLoginResult.Succeeded);

        var linkedUser = await userManager.FindByLoginAsync("Google", "google-sub-id-99999");
        Assert.NotNull(linkedUser);
        Assert.Equal(existingUser.Id, linkedUser.Id);

        var userLogins = await userManager.GetLoginsAsync(existingUser);
        Assert.Single(userLogins);
        Assert.Equal("Google", userLogins[0].LoginProvider);
        Assert.Equal("google-sub-id-99999", userLogins[0].ProviderKey);
    }

    [Fact]
    public async Task ExternalLogin_DuplicateLoginProviderKey_CannotBeBoundToMultipleUsers()
    {
        using var provider = BuildTestServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();

        var user1 = new IdentityUser { UserName = "user1@example.com", Email = "user1@example.com", EmailConfirmed = true };
        var user2 = new IdentityUser { UserName = "user2@example.com", Email = "user2@example.com", EmailConfirmed = true };

        await userManager.CreateAsync(user1, "Password123!");
        await userManager.CreateAsync(user2, "Password123!");

        var loginInfo = new UserLoginInfo("Google", "shared-provider-key", "Google");

        var result1 = await userManager.AddLoginAsync(user1, loginInfo);
        Assert.True(result1.Succeeded);

        // Trying to bind the exact same provider key to user2 should fail
        var result2 = await userManager.AddLoginAsync(user2, loginInfo);
        Assert.False(result2.Succeeded);
    }

    [Fact]
    public async Task ExternalLogin_UnconfirmedUser_CannotSignInWhenRequireConfirmedAccountIsTrue()
    {
        using var provider = BuildTestServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var unconfirmedUser = new IdentityUser
        {
            UserName = "unconfirmed@example.com",
            Email = "unconfirmed@example.com",
            EmailConfirmed = false
        };

        await userManager.CreateAsync(unconfirmedUser);
        var loginInfo = new UserLoginInfo("Google", "google-unconfirmed-sub", "Google");
        await userManager.AddLoginAsync(unconfirmedUser, loginInfo);

        var canSignIn = await signInManager.CanSignInAsync(unconfirmedUser);
        Assert.False(canSignIn);
    }

    [Fact]
    public void ExternalLoginModel_OnGet_RedirectsToLoginPage()
    {
        using var provider = BuildTestServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();
        var userStore = provider.GetRequiredService<IUserStore<IdentityUser>>();

        var model = new ExternalLoginModel(
            signInManager,
            userManager,
            userStore,
            NullLogger<ExternalLoginModel>.Instance);

        var result = model.OnGet();
        var redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./Login", redirectResult.PageName);
    }

    [Fact]
    public async Task ExternalLogin_SynchronizeCustomClaims_UpdatesChangedClaims()
    {
        using var provider = BuildTestServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "avatar_user@example.com", Email = "avatar_user@example.com", EmailConfirmed = true };
        await userManager.CreateAsync(user);

        // Initial claims
        await userManager.AddClaimAsync(user, new Claim("urn:google:picture", "https://old.url/photo.jpg"));
        await userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, "OldName"));

        // Simulate updated claims coming from Google
        var initialClaims = await userManager.GetClaimsAsync(user);
        var oldPicClaim = initialClaims.First(c => c.Type == "urn:google:picture");
        var oldNameClaim = initialClaims.First(c => c.Type == ClaimTypes.GivenName);

        await userManager.RemoveClaimAsync(user, oldPicClaim);
        await userManager.AddClaimAsync(user, new Claim("urn:google:picture", "https://new.url/photo.jpg"));

        await userManager.RemoveClaimAsync(user, oldNameClaim);
        await userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, "NewName"));

        var updatedClaims = await userManager.GetClaimsAsync(user);
        Assert.Contains(updatedClaims, c => c.Type == "urn:google:picture" && c.Value == "https://new.url/photo.jpg");
        Assert.DoesNotContain(updatedClaims, c => c.Type == "urn:google:picture" && c.Value == "https://old.url/photo.jpg");
        Assert.Contains(updatedClaims, c => c.Type == ClaimTypes.GivenName && c.Value == "NewName");
        Assert.DoesNotContain(updatedClaims, c => c.Type == ClaimTypes.GivenName && c.Value == "OldName");
    }
}
