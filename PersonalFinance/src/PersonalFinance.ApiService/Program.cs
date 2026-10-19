using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

// 1. Bootstrap early logging to capture any startup or DI registration failures
Log.Logger = new LoggerConfiguration()
    .WriteTo.Async(a => a.Console(
        theme: AnsiConsoleTheme.Code,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting {AppName} (v{Version})...", AppVersion.ApplicationName, AppVersion.Current);

    var builder = WebApplication.CreateBuilder(args);
    AppCulture.ApplyDefault();

    // 1a. Load user secrets in Development environment for local credential management
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
    }

    // 2. Add Aspire service defaults (OpenTelemetry, Health Checks, Service Discovery, Resilience)
    builder.AddServiceDefaults();

    // 3. Configure Serilog full logging pipeline with non-blocking async console sink, rolling file sink (30-day retention), and OpenTelemetry forwarding
    builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .WriteTo.Async(a => a.File(
                path: Path.Combine("logs", "apiservice-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")),
        writeToProviders: true);

    // 3. Register EF Core DbContext with environment-driven provider (Azure SQL Serverless / SQLite)
    builder.Services.AddAppDbContext(builder.Configuration);

    // 3a. Register ASP.NET Core Identity for user management in API controllers
    builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AppPolicies.RequireAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.Admin));

    // 3b. Register X-User-Id header-based authentication for internal service-to-service calls
    builder.Services.AddAuthentication(XUserIdAuthenticationDefaults.Scheme)
        .AddScheme<AuthenticationSchemeOptions, XUserIdAuthenticationHandler>(XUserIdAuthenticationDefaults.Scheme,
            options => { });

    // 4. Standard RFC 7807 ProblemDetails for standardized error responses
    builder.Services.AddProblemDetails();

    // 5. Health Checks for container orchestrators (Kubernetes / Docker) and load balancers
    builder.Services.AddHealthChecks();

    // 5b. Register Google Drive connection management service with typed HttpClient
    builder.Services.AddHttpClient<IDriveConnectionService, DriveConnectionService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // 5c. Register Spreadsheet Reader service with typed HttpClient
    builder.Services.AddHttpClient<ISpreadsheetReader, SpreadsheetReaderService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // 5a. Register Net Worth parameters persistence service
    builder.Services.AddScoped<INetWorthParametersService, NetWorthParametersService>();

    // 5b. Register Personal Finance services
    builder.Services.AddScoped<IBudgetService, BudgetService>();
    builder.Services.AddScoped<IBudgetDataSourceService, BudgetDataSourceService>();
    builder.Services.AddScoped<SheetBudgetMonthReader>();
    builder.Services.AddScoped<TrackerBudgetMonthReader>();
    builder.Services.AddScoped<IBudgetDataProvider, BudgetDataProvider>();
    builder.Services.AddScoped<IMonthlyBudgetService, MonthlyBudgetService>();
    builder.Services.AddScoped<ISavingsGoalService, SavingsGoalService>();
    builder.Services.AddScoped<IWatchlistService, WatchlistService>();
    builder.Services.AddScoped<ICommunityService, CommunityService>();
    builder.Services.AddScoped<ISpreadsheetAnalysisService, SpreadsheetAnalysisService>(sp =>
    {
        var dbContext = sp.GetRequiredService<AppDbContext>();
        var spreadsheetReader = sp.GetRequiredService<ISpreadsheetReader>();
        var notificationService = sp.GetRequiredService<INotificationService>();
        var budgetDataProvider = sp.GetRequiredService<IBudgetDataProvider>();
        return new SpreadsheetAnalysisService(dbContext, spreadsheetReader, notificationService, budgetDataProvider);
    });

    // 5c. Register AI Assistant service for OpenAI chat integration
    builder.Services.AddScoped<IAiAssistantService, AiAssistantService>();

    // 5d. Register Dashboard service
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    // 5d-1. Register Admin service (system-level counts for admin-only endpoints)
    builder.Services.AddScoped<IAdminService, AdminService>();

    // 5e. Register Notification service
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // 5f. Register Search service
    builder.Services.AddScoped<ISearchService, SearchService>();

    // 5f. Register Rate Limiter service for per-user request throttling
    builder.Services.AddSingleton<IRateLimiterService, RateLimiterService>();

    // 5d. Register FluentValidation - scans the assembly for validators
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddControllers()
        .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

    // 6. OpenAPI generator in .NET 10 with synchronized application metadata
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

    var app = builder.Build();

    // 7. Global Exception Handling via ProblemDetails
    app.UseExceptionHandler();

    // 8. Enable Serilog HTTP request logging with request duration & status codes
    app.UseSerilogRequestLogging();

    // 9. Map default Aspire endpoints (/health, /alive)
    app.MapDefaultEndpoints();

    // 10. Root Info & Version Endpoint (Instant Runtime Verification)
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

    // 11. API documentation tooling (OpenAPI spec & Scalar UI) - always in Development,
    //     opt-in elsewhere via ApiDocs:Enabled (set when the API is exposed for testing)
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApiDocs:Enabled"))
    {
        // Generates the OpenAPI spec endpoint at /openapi/v1.json
        app.MapOpenApi();

        // Generates the interactive Scalar API Reference UI at /scalar/v1
        app.MapScalarApiReference(options =>
        {
            options.WithTitle($"{AppVersion.ApplicationName} API Reference (v{AppVersion.Current})")
                .WithTheme(ScalarTheme.Moon);
        });
    }

    // 12. Auto-migrate database schema and seed initial sample data across environments
    if (args.Contains("--migrate-only") ||
        string.Equals(Environment.GetEnvironmentVariable("MIGRATE_ONLY"), "true", StringComparison.OrdinalIgnoreCase))
    {
        Log.Information("Executing database migration task and exiting (--migrate-only)...");
        await app.MigrateAndSeedDatabaseAsync();
        Log.Information("Database migration task finished successfully.");
        return;
    }

    await app.MigrateAndSeedDatabaseAsync();

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

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