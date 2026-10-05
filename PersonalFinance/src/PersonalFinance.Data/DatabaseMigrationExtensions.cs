using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinance.Data.Entities;

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
        var delay = initialDelay ?? TimeSpan.FromSeconds(3);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger ??= scope.ServiceProvider.GetService<ILogger<AppDbContext>>();

        var providerName = db.Database.ProviderName ?? "Unknown";
        logger?.LogInformation("Starting database migration and initialization for provider: {Provider}...", providerName);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Apply pending EF Core migrations
                var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count > 0)
                {
                    logger?.LogInformation(
                        "Applying {Count} pending migration(s): {Migrations}",
                        pendingMigrations.Count,
                        string.Join(", ", pendingMigrations));

                    await db.Database.MigrateAsync(cancellationToken);
                    logger?.LogInformation("Database schema successfully migrated.");
                }
                else
                {
                    // Ensure database exists if no migrations are pending (e.g. up to date)
                    await db.Database.MigrateAsync(cancellationToken);
                    logger?.LogInformation("Database schema is up to date (no pending migrations).");
                }

                // Seed initial sample data if items table is empty
                await SeedInitialDataAsync(db, logger, cancellationToken);

                logger?.LogInformation("Database migration and initialization completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                if (db.Database.IsSqlite() && ex.ToString().Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogWarning(
                        ex,
                        "Detected legacy or incompatible SQLite database tables without matching migration history. Resetting SQLite schema to apply provider migrations...");

                    try
                    {
                        await ResetSqliteDatabaseAsync(db, cancellationToken);
                        await db.Database.MigrateAsync(cancellationToken);
                        logger?.LogInformation("Database schema successfully migrated after SQLite schema reset.");

                        await SeedInitialDataAsync(db, logger, cancellationToken);
                        logger?.LogInformation("Database migration and initialization completed successfully.");
                        return;
                    }
                    catch (Exception resetEx)
                    {
                        logger?.LogError(resetEx, "Failed while attempting to reset and migrate SQLite database schema.");
                    }
                }

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
    /// Resets all tables in a SQLite database when an orphaned schema without EF migration history is detected.
    /// </summary>
    private static async Task ResetSqliteDatabaseAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var pragmaOff = connection.CreateCommand();
            pragmaOff.CommandText = "PRAGMA foreign_keys = OFF;";
            await pragmaOff.ExecuteNonQueryAsync(cancellationToken);

            var tables = new List<string>();
            using (var getTablesCmd = connection.CreateCommand())
            {
                getTablesCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
                using var reader = await getTablesCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            foreach (var table in tables)
            {
                using var dropCmd = connection.CreateCommand();
                dropCmd.CommandText = $"DROP TABLE IF EXISTS \"{table}\";";
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            using var pragmaOn = connection.CreateCommand();
            pragmaOn.CommandText = "PRAGMA foreign_keys = ON;";
            await pragmaOn.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
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
        var logger = host.Services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseInitializer");
        await host.Services.MigrateAndSeedDatabaseAsync(logger, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Seeds initial default items if none exist in the database.
    /// </summary>
    private static async Task SeedInitialDataAsync(
        AppDbContext db,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (await db.Items.AnyAsync(cancellationToken))
        {
            return;
        }

        logger?.LogInformation("Seeding initial starter items...");

        var defaultItems = new[]
        {
            new Item
            {
                Name = "Getting Started with Refit & OpenAPI",
                Description = "Explore the type-safe Refit client in Web consuming ApiService endpoints.",
                IsCompleted = true,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
            },
            new Item
            {
                Name = "Configure Authentication Modes",
                Description = "Test Individual Identity or Windows Negotiate authentication scheme across services.",
                IsCompleted = false,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-45)
            },
            new Item
            {
                Name = "Verify Interactive Scalar UI & Health Checks",
                Description = "Access /scalar/v1 and /health endpoints to validate runtime service health.",
                IsCompleted = false,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-15)
            }
        };

        await db.Items.AddRangeAsync(defaultItems, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Seeded {Count} initial items into database.", defaultItems.Length);
    }
}
