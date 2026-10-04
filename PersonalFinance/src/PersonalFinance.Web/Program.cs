using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Refit;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Web.Services;

// 1. Bootstrap early logging to catch startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(
        theme: AnsiConsoleTheme.Code,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting {AppName} (v{Version})...", AppVersion.ApplicationName, AppVersion.Current);

    var builder = WebApplication.CreateBuilder(args);

    // 2. Add Aspire service defaults (OpenTelemetry, Health Checks, Service Discovery, Resilience)
    builder.AddServiceDefaults();

    // 3. Wire up Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            theme: AnsiConsoleTheme.Code,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // 3a. Register EF Core DbContext & ASP.NET Core Identity (shared solution-level database for local development)
    var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                              ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    var connectionString = DatabasePathHelper.ResolveConnectionString(rawConnectionString);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));

    builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>();

    // 3b. Configure Identity Application Cookie with .NET 10 API-Aware Redirect Handling
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

    // 3c. Register Google External Authentication if configured
    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
    {
        builder.Services.AddAuthentication()
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = true;

                // Request standard OpenID Connect profile scopes
                options.Scope.Add("profile");
                options.Scope.Add("email");

                // Map custom claims from Google's JSON userinfo payload
                options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
                options.ClaimActions.MapJsonKey("urn:google:locale", "locale", "string");
                options.ClaimActions.MapJsonKey("urn:google:verified_email", "email_verified", "bool");
            });
    }

    // 3d. Register Brevo Email Sender
    builder.Services.Configure<BrevoOptions>(
        builder.Configuration.GetSection("Brevo"));
    builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddRazorPages();

    // 4. Add MVC Controllers and Views
    builder.Services.AddControllersWithViews();
    builder.Services.AddProblemDetails();

    // 5. Register Health Checks
    builder.Services.AddHealthChecks();

    // 6. Register Refit Clients with Service Discovery
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https+http://apiservice";

    builder.Services.AddRefitClient<IGoogleDriveApi>()
        .ConfigureHttpClient(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(35);
        });

    var app = builder.Build();

    // 7. Enable Serilog HTTP request logging
    app.UseSerilogRequestLogging();

    // 8. Map default Aspire endpoints (/health, /alive)
    app.MapDefaultEndpoints();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler();
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapStaticAssets();

    app.MapControllerRoute(
            name: "default",
            pattern: "{controller=GoogleDrive}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.MapRazorPages();

    if (app.Environment.IsDevelopment())
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "Could not automatically initialize Identity database on startup. Verify database connection string.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}