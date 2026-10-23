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
    /// SQL Server engine through the EF Core SQL Server provider: Microsoft Azure SQL Database in production,
    /// and local SQL Server in development (the Docker container from <c>compose.yaml</c>, a native instance or LocalDB).
    /// Both use the same migrations; only the connection string differs.
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
    /// Default fallback local SQL Server connection string, used when no connection string is configured.
    /// It uses Windows authentication (<c>Trusted_Connection</c>) on <c>localhost,1433</c>, so it only works on Windows
    /// with a native SQL Server instance. Everyone else, including users of the local SQL Server Docker container,
    /// must set <c>ConnectionStrings:DefaultConnection</c> in dotnet user-secrets.
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
        string? explicitProvider = configuration?[DatabaseProviderConfigKey]
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
            string trimmed = explicitProvider.Trim();
            if (trimmed.Equals("InMemory", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Memory", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.InMemory;
            }
        }

        if (!string.IsNullOrWhiteSpace(connectionStringFallback))
        {
            string trimmed = connectionStringFallback.Trim();
            if (trimmed.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.InMemory;
            }
        }

        return DatabaseProviderType.AzureSql;
    }

    /// <summary>
    /// Resolves connection string, falling back to <see cref="DefaultSqlConnectionString"/> if empty.
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
            string resolvedConnectionString = ResolveConnectionString(connectionString);
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
        string rawConnectionString = configuration.GetConnectionString(connectionStringName)
            ?? configuration[connectionStringName]
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionStringName}")
            ?? Environment.GetEnvironmentVariable(connectionStringName)
            ?? DefaultSqlConnectionString;

        DatabaseProviderType providerType = DetermineProvider(configuration, rawConnectionString);
        string resolvedConnectionString = ResolveConnectionString(rawConnectionString);

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
        string resolvedConnectionString = ResolveConnectionString(connectionString);

        services.AddDbContext<AppDbContext, AzureSqlAppDbContext>(options =>
        {
            options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction);
        });

        return services;
    }
}
