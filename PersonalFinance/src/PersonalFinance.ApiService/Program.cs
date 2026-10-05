using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;

// 1. Bootstrap early logging to capture any startup or DI registration failures
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

    // 3. Configure Serilog full logging pipeline from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            theme: AnsiConsoleTheme.Code,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // 3. Register EF Core DbContext with environment-driven provider (Azure SQL Serverless / SQLite)
    builder.Services.AddAppDbContext(builder.Configuration);

    // 4. Standard RFC 7807 ProblemDetails for standardized error responses
    builder.Services.AddProblemDetails();

    // 5. Health Checks for container orchestrators (Kubernetes / Docker) and load balancers
    builder.Services.AddHealthChecks();

    // 5b. Register Google Drive integration service with typed HttpClient
    builder.Services.AddHttpClient<IGoogleDriveService, GoogleDriveService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddControllers();

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

    // 11. Development tooling (OpenAPI spec & Scalar UI)
    if (app.Environment.IsDevelopment())
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