using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.ApiService.Features.AccountLifecycle;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.BudgetPlanner;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Features.Community;
using PersonalFinance.ApiService.Features.Dashboard;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Features.MoneyPriorities;
using PersonalFinance.ApiService.Features.NetWorth;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Features.Search;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using Scalar.AspNetCore;
using Serilog;

// Console logging until the host is built, so startup and DI failures are captured.
Log.Logger = LoggingExtensions.CreateBootstrapLogger();

// Captured once the host is built, so a cancellation during shutdown is not reported as a crash.
CancellationToken applicationStopping = CancellationToken.None;

try
{
    Log.Information("Starting {AppName} (v{Version})...", AppVersion.ApplicationName, AppVersion.Current);

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    AppCulture.ApplyDefault();

    // Platform: Aspire defaults (OpenTelemetry, health checks, service discovery, HttpClient resilience) and Serilog.
    builder.AddServiceDefaults();
    builder.AddAppLogging(logFilePrefix: "apiservice");

    // Data: EF Core (Azure SQL, or InMemory for tests/e2e) and the Identity stores used to read Roles.
    builder.Services.AddAppDbContext(builder.Configuration);
    builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();

    // Security: Web proves the caller with X-User-Id + X-Internal-Key (ADR 0004); code checks Policies, never Roles.
    builder.Services.AddAuthentication(XUserIdAuthenticationDefaults.Scheme)
        .AddScheme<AuthenticationSchemeOptions, XUserIdAuthenticationHandler>(XUserIdAuthenticationDefaults.Scheme,
            _ => { });
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AppPolicies.RequireAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.Admin));

    // Features: each one registers itself from Features/<Feature>/<Feature>Feature.cs (ADR 0007).
    builder.Services
        .AddApiServiceInfrastructure()
        .AddNetWorthFeature()
        .AddBudgetPlannerFeature()
        .AddBudgetTrackerFeature()
        .AddAnalysisFeature()
        .AddGoogleDriveFeature()
        .AddDashboardFeature()
        .AddSearchFeature()
        .AddSavingsGoalsFeature()
        .AddCommunityFeature()
        .AddWatchlistFeature()
        .AddResearchFeature(builder.Configuration, builder.Environment)
        .AddAiAssistantFeature(builder.Configuration)
        .AddFeedbackFeature()
        .AddAdminFeature()
        .AddAccountLifecycleFeature()
        .AddMoneyPrioritiesFeature();

    // HTTP: controllers, FluentValidation validators, RFC 7807 ProblemDetails, OpenAPI document.
    builder.Services.AddControllers()
        .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
    builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = $"{AppVersion.ApplicationName} API Service";
            document.Info.Version = AppVersion.Current;
            document.Info.Description =
                $"REST API backend for {AppVersion.ApplicationName} (SemVer: {AppVersion.FullVersion}).";
            return Task.CompletedTask;
        });
    });

    WebApplication app = builder.Build();
    applicationStopping = app.Lifetime.ApplicationStopping;

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    // Anonymous infrastructure endpoints: /health, /alive and the version banner at /.
    app.MapDefaultEndpoints();
    app.MapGet("/", () => Results.Ok(new
    {
        Application = AppVersion.ApplicationName,
        Version = AppVersion.Current,
        FullVersion = AppVersion.FullVersion,
        Environment = app.Environment.EnvironmentName,
        Status = "Online",
        TimestampUtc = DateTime.UtcNow
    }))
        .WithName("GetVersionInfo")
        .WithSummary("Returns current API service version and runtime status")
        .WithTags("System");

    // API documentation (/openapi/v1.json, /scalar/v1): always in Development, opt-in elsewhere via ApiDocs:Enabled.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApiDocs:Enabled"))
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle($"{AppVersion.ApplicationName} API Reference (v{AppVersion.Current})")
                .WithTheme(ScalarTheme.Moon);
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // The daily Purge job runs this image with --purge-only (ADR 0005): migrate, purge, exit.
    if (AccountPurgeJob.IsRequested(args))
    {
        return await AccountPurgeJob.RunAsync(app);
    }

    // The daily Research refresh job runs this image with --refresh-research: migrate, rebuild the snapshots, exit.
    if (ResearchRefreshJob.IsRequested(args))
    {
        return await ResearchRefreshJob.RunAsync(app);
    }

    // E2E only: Development with E2E_SEED_RESEARCH=true stores small Research fixture snapshots; never in Production.
    await ResearchFixtureSeeder.SeedIfEnabledAsync(app);

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
