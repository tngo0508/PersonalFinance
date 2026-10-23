using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;
using Serilog;

// Console logging until the host is built, so startup and DI failures are captured.
Log.Logger = LoggingExtensions.CreateBootstrapLogger();

// Captured once the host is built, so a cancellation during shutdown is not reported as a crash.
CancellationToken applicationStopping = CancellationToken.None;

try
{
    Log.Information("Starting {AppName} (v{Version})...", AppVersion.ApplicationName, AppVersion.Current);

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Pin culture to en-US (chiseled containers start in the invariant culture and format currency as "¤").
    AppCulture.ApplyDefault();

    // Platform: Aspire defaults (OpenTelemetry, health checks, service discovery, HttpClient resilience) and Serilog.
    builder.AddServiceDefaults();
    builder.AddAppLogging(logFilePrefix: "web");

    // Data: EF Core for ASP.NET Core Identity, consent records and Data Protection keys.
    builder.Services.AddAppDbContext(builder.Configuration);
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<AppDbContext>(); // keeps cookies and antiforgery tokens valid across container restarts

    // Identity: CI/E2E runs disable email confirmation so a new account can sign in immediately.
    bool disableEmailConfirmationForE2E = Environment.GetEnvironmentVariable("E2E_DISABLE_EMAIL_CONFIRMATION") == "true";
    builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = !disableEmailConfirmationForE2E;
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        ApiAwareCookieRedirects.Apply(options.Events); // 401/403 instead of a login redirect for fetch/XHR calls
    });

    // Google sign-in, only when its credentials are configured.
    string? googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    string? googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
    {
        builder.Services.AddAuthentication()
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
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

    // Authorization: Policies, never Role names (ADR 0004); non-admins get 404, not 403, on admin pages.
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AppPolicies.RequireAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.Admin));
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AdminNotFoundAuthorizationResultHandler>();

    // ApiService: one Refit client per contract, each forwarding the signed-in Account (Infrastructure/InternalApiClientExtensions.cs).
    builder.Services.AddInternalApiClients(builder.Configuration);

    // Email sent from the Identity pages, through Brevo.
    builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));
    builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(15));

    // UI services (Services/, ADR 0008): the Consent an Account gives to the Terms of Service and Privacy Policy at
    // registration.
    builder.Services.AddScoped<IUserConsentService, UserConsentService>();

    // The clock, injected so tests can fix the time (CODING_STANDARDS "Async and time").
    builder.Services.AddSingleton(TimeProvider.System);

    // Hosting behind Azure Container Apps: trust X-Forwarded-For/Proto so HTTPS and OAuth redirect URIs are correct.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Lock request localization to en-US so a browser Accept-Language header cannot change currency or number formats.
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture(AppCulture.DefaultCultureName);
        options.SupportedCultures = [AppCulture.Default];
        options.SupportedUICultures = [AppCulture.Default];
    });

    // MVC and Razor Pages; antiforgery tokens are validated on every unsafe (POST/PUT/PATCH/DELETE) action by default.
    builder.Services.AddRazorPages();
    builder.Services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });
    builder.Services.AddProblemDetails();

    WebApplication app = builder.Build();
    applicationStopping = app.Lifetime.ApplicationStopping;

    app.UseSerilogRequestLogging();
    app.MapDefaultEndpoints();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler();
        app.UseHsts();
    }

    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    app.UseRequestLocalization();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapStaticAssets();

    app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();
    app.MapRazorPages();

    await app.MigrateAndRunAsync(args);
    return StartupExit.Success;
}
catch (Exception ex) when (StartupExit.IsShutdown(ex, applicationStopping))
{
    return StartupExit.Success;
}
catch (Exception ex)
{
    // Non-zero exit so --migrate-only jobs, containers and CI see the failure.
    Log.Fatal(ex, "Application terminated unexpectedly.");
    return StartupExit.Failure;
}
finally
{
    await Log.CloseAndFlushAsync();
}
