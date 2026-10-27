using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersonalFinance.Data;

/// <summary>
/// Provides extension methods for safe, automated EF Core schema migrations and data seeding on application startup or ACA job initialization.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Executes EF Core migrations with resilience/retry logic and seeds initial data if missing.
    /// </summary>
    public static async Task MigrateAndSeedDatabaseAsync(
        this IServiceProvider serviceProvider,
        ILogger? logger = null,
        int maxRetries = 5,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan delay = initialDelay ?? TimeSpan.FromSeconds(3);

        using IServiceScope scope = serviceProvider.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger ??= scope.ServiceProvider.GetService<ILogger<AppDbContext>>();

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            string providerName = db.Database.ProviderName ?? "Unknown";
            string targetHost = db.Database.IsRelational() ? db.Database.GetDbConnection().DataSource : "(in-memory)";
            logger.LogInformation(
                "Starting database migration and initialization for provider: {Provider} on target host: {TargetHost}...",
                providerName,
                string.IsNullOrWhiteSpace(targetHost) ? "(unspecified/localhost)" : targetHost);
        }

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!db.Database.IsRelational())
                {
                    await db.Database.EnsureCreatedAsync(cancellationToken);
                    logger?.LogInformation("Non-relational database schema initialized.");
                }
                else
                {
                    // Apply pending EF Core migrations
                    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                    if (pendingMigrations.Count > 0)
                    {
                        if (logger is not null && logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "Applying {Count} pending migration(s): {Migrations}",
                                pendingMigrations.Count,
                                string.Join(", ", pendingMigrations));
                        }

                        await db.Database.MigrateAsync(cancellationToken);
                        logger?.LogInformation("Database schema successfully migrated.");
                    }
                    else
                    {
                        // Ensure database exists if no migrations are pending (e.g. up to date)
                        await db.Database.MigrateAsync(cancellationToken);
                        logger?.LogInformation("Database schema is up to date (no pending migrations).");
                    }
                }

                // Seed Identity roles and the configured Admin grant
                await RbacSeeder.SeedAsync(scope.ServiceProvider, logger, cancellationToken);

                logger?.LogInformation("Database migration and initialization completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning(
                    ex,
                    "Database migration attempt {Attempt} of {MaxRetries} failed. Retrying in {DelaySeconds}s (waiting for Serverless/Database ready state)...",
                    attempt,
                    maxRetries,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30));
            }
        }
    }

    /// <summary>
    /// Executes database migration and seeding for an IHost / WebApplication instance.
    /// </summary>
    public static async Task MigrateAndSeedDatabaseAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ILogger? logger = host.Services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseInitializer");
        await host.Services.MigrateAndSeedDatabaseAsync(logger, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Applies pending migrations and seed data, then runs the host until shutdown.
    /// With the <c>--migrate-only</c> argument or <c>MIGRATE_ONLY=true</c> it exits after migrating
    /// (used by Azure Container Apps jobs and init containers).
    /// </summary>
    public static async Task MigrateAndRunAsync(this IHost host, string[] args)
    {
        bool migrateOnly = args.Contains("--migrate-only") ||
                          string.Equals(Environment.GetEnvironmentVariable("MIGRATE_ONLY"), "true",
                              StringComparison.OrdinalIgnoreCase);

        await host.MigrateAndSeedDatabaseAsync();

        if (migrateOnly)
        {
            host.Services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseInitializer")
                .LogInformation("Database migration task finished (--migrate-only); exiting.");
            return;
        }

        await host.RunAsync();
    }
}
