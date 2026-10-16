using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinance.Data;

/// <summary>
/// Supported database provider types.
/// </summary>
public enum DatabaseProviderType
{
    /// <summary>
    /// Microsoft Azure SQL Database (Serverless Free Tier / General Purpose).
    /// </summary>
    AzureSql,

    /// <summary>
    /// In-memory database for lightweight fast test execution.
    /// </summary>
    InMemory
}

/// <summary>
/// Helper to manage database connection strings, EF Core options configuration, and service registration.
/// </summary>
public static class DatabaseProviderHelper
{
    public const string DefaultConnectionStringName = "DefaultConnection";
    public const string DatabaseProviderConfigKey = "Database:Provider";
    public const string DatabaseProviderEnvVar = "DATABASE_PROVIDER";

    /// <summary>
    /// Default fallback SQL Server connection string for local development when unconfigured.
    /// In development, sensitive credentials should be configured using dotnet user-secrets.
    /// In production/cloud deployments, connection strings should be provided via environment variables or secret stores.
    /// </summary>
    public const string DefaultSqlConnectionString =
        "Server=localhost,1433;Database=PersonalFinance;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    /// <summary>
    /// Determines the database provider based on explicit configuration/env vars or connection string heuristics.
    /// Defaults to <see cref="DatabaseProviderType.AzureSql"/>.
    /// </summary>
    public static DatabaseProviderType DetermineProvider(IConfiguration? configuration, string? connectionString)
    {
        var explicitProvider = configuration?[DatabaseProviderConfigKey]
            ?? configuration?["DatabaseProvider"]
            ?? configuration?["DB_PROVIDER"]
            ?? Environment.GetEnvironmentVariable(DatabaseProviderEnvVar)
            ?? Environment.GetEnvironmentVariable("DB_PROVIDER");

        return ParseProviderName(explicitProvider, connectionString);
    }

    /// <summary>
    /// Parses an explicit provider name into <see cref="DatabaseProviderType"/>.
    /// </summary>
    public static DatabaseProviderType ParseProviderName(string? explicitProvider, string? connectionStringFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            var trimmed = explicitProvider.Trim();
            if (trimmed.Equals("InMemory", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Memory", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.InMemory;
            }
        }

        if (!string.IsNullOrWhiteSpace(connectionStringFallback))
        {
            var trimmed = connectionStringFallback.Trim();
            if (trimmed.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.InMemory;
            }
        }

        return DatabaseProviderType.AzureSql;
    }

    /// <summary>
    /// Resolves connection string, falling back to the configured default Azure SQL connection string if empty.
    /// </summary>
    public static string ResolveConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DefaultSqlConnectionString;
        }

        return connectionString.Trim();
    }

    /// <summary>
    /// Configures DbContextOptionsBuilder with SQL Server or In-Memory provider and resilience policies.
    /// </summary>
    public static DbContextOptionsBuilder ConfigureAppDbContext(
        this DbContextOptionsBuilder options,
        string connectionString,
        DatabaseProviderType providerType = DatabaseProviderType.AzureSql,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
    {
        if (providerType == DatabaseProviderType.InMemory)
        {
            options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(connectionString) ? "PersonalFinanceInMemory" : connectionString);
        }
        else
        {
            var resolvedConnectionString = ResolveConnectionString(connectionString);
            options.UseSqlServer(resolvedConnectionString, sqlServerOptions =>
            {
                // Enable resilient execution strategy for Azure SQL Serverless auto-pause resumption and transient network drops
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                sqlServerOptionsAction?.Invoke(sqlServerOptions);
            });

            // Suppress PendingModelChangesWarning as an error during startup migrations.
            // When the model drifts from the last migration (e.g., after a refactoring), EF Core 9+
            // throws by default. We log it as a warning instead so the application can still start
            // and apply existing migrations. A new migration should be generated before deployment.
            options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
        }

        return options;
    }

    /// <summary>
    /// Registers <see cref="AppDbContext"/> and <see cref="AzureSqlAppDbContext"/> with Azure SQL database provider.
    /// </summary>
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = DefaultConnectionStringName,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
    {
        var rawConnectionString = configuration.GetConnectionString(connectionStringName)
            ?? configuration[connectionStringName]
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionStringName}")
            ?? Environment.GetEnvironmentVariable(connectionStringName)
            ?? DefaultSqlConnectionString;

        var providerType = DetermineProvider(configuration, rawConnectionString);
        var resolvedConnectionString = ResolveConnectionString(rawConnectionString);

        services.AddDbContext<AppDbContext, AzureSqlAppDbContext>(options =>
        {
            options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="AppDbContext"/> with a specific connection string for tests or custom hosts.
    /// </summary>
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        string connectionString,
        DatabaseProviderType providerType = DatabaseProviderType.AzureSql,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
    {
        var resolvedConnectionString = ResolveConnectionString(connectionString);

        services.AddDbContext<AppDbContext, AzureSqlAppDbContext>(options =>
        {
            options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction);
        });

        return services;
    }
}
