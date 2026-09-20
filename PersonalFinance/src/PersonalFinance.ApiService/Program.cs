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

    // 2. Configure Serilog full logging pipeline from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            theme: AnsiConsoleTheme.Code,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // 3. Register EF Core DbContext with SQLite (shared solution-level database for local development)
    var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                              ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    var connectionString = DatabasePathHelper.ResolveConnectionString(rawConnectionString);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));

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

    // 9. Root Info & Version Endpoint (Instant Runtime Verification)
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

    // 10. Health check endpoint
    app.MapHealthChecks("/health")
        .WithName("HealthCheck")
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

        // Auto-migrate database schema and seed initial sample data for local development
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            if (!db.Items.Any())
            {
                db.Items.AddRange(
                    new PersonalFinance.Data.Entities.Item
                    {
                        Name = "Getting Started with Refit & OpenAPI",
                        Description = "Explore the type-safe Refit client in Web consuming ApiService endpoints.",
                        IsCompleted = true,
                        CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
                    },
                    new PersonalFinance.Data.Entities.Item
                    {
                        Name = "Configure Authentication Modes",
                        Description =
                            "Test Individual Identity or Windows Negotiate authentication scheme across services.",
                        IsCompleted = false,
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-45)
                    },
                    new PersonalFinance.Data.Entities.Item
                    {
                        Name = "Verify Interactive Scalar UI & Health Checks",
                        Description = "Access /scalar/v1 and /health endpoints to validate runtime service health.",
                        IsCompleted = false,
                        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-15)
                    }
                );
                db.SaveChanges();
                Log.Information("Database initialized and seeded with sample items.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "Could not automatically initialize the database on startup. Verify database connection string.");
        }
    }

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