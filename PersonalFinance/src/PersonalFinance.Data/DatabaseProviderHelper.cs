using Microsoft.EntityFrameworkCore;
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
    /// Local or persistent SQLite database file.
    /// </summary>
    Sqlite,

    /// <summary>
    /// Microsoft Azure SQL Database (Serverless Free Tier / General Purpose) or on-prem SQL Server.
    /// </summary>
    SqlServer
}

/// <summary>
/// Helper to manage environment-driven database provider resolution, connection strings, and EF Core options configuration.
/// Each provider uses its own EF Core migration set (<c>Migrations/Sqlite</c> vs <c>Migrations/SqlServer</c>).
/// </summary>
public static class DatabaseProviderHelper
{
    public const string DefaultConnectionStringName = "DefaultConnection";
    public const string DatabaseProviderConfigKey = "Database:Provider";
    public const string DatabaseProviderEnvVar = "DATABASE_PROVIDER";

    /// <summary>
    /// Determines the database provider based on explicit configuration/env vars or connection string heuristics.
    /// </summary>
    public static DatabaseProviderType DetermineProvider(IConfiguration? configuration, string? connectionString)
    {
        // 1. Explicit configuration key or environment variable
        var explicitProvider = configuration?[DatabaseProviderConfigKey]
            ?? configuration?["DatabaseProvider"]
            ?? configuration?["DB_PROVIDER"]
            ?? Environment.GetEnvironmentVariable(DatabaseProviderEnvVar)
            ?? Environment.GetEnvironmentVariable("DB_PROVIDER");

        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            var trimmed = explicitProvider.Trim();
            if (trimmed.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("MSSQL", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("AzureSql", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("SqlAzure", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.SqlServer;
            }

            if (trimmed.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.Sqlite;
            }
        }

        // 2. Connection string heuristic auto-detection
        return DetectProviderFromConnectionString(connectionString);
    }

    /// <summary>
    /// Parses an explicit provider name into <see cref="DatabaseProviderType"/>.
    /// </summary>
    public static DatabaseProviderType ParseProviderName(string? explicitProvider, string? connectionStringFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            var trimmed = explicitProvider.Trim();
            if (trimmed.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("MSSQL", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("AzureSql", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("SqlAzure", StringComparison.OrdinalIgnoreCase) ||
                (trimmed.StartsWith("Sql", StringComparison.OrdinalIgnoreCase)
                 && !trimmed.StartsWith("Sqlite", StringComparison.OrdinalIgnoreCase)))
            {
                return DatabaseProviderType.SqlServer;
            }

            if (trimmed.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseProviderType.Sqlite;
            }
        }

        return DetectProviderFromConnectionString(connectionStringFallback);
    }

    /// <summary>
    /// Detects the database provider from connection string syntax.
    /// </summary>
    public static DatabaseProviderType DetectProviderFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DatabaseProviderType.Sqlite;
        }

        var normalized = connectionString.Trim();

        // SQL Server / Azure SQL connection string indicators
        if (normalized.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Data Source=tcp:", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("User ID=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("MultipleActiveResultSets=", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Application Name=", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProviderType.SqlServer;
        }

        return DatabaseProviderType.Sqlite;
    }

    /// <summary>
    /// Resolves connection string based on provider type.
    /// For SQLite, ensures paths are anchored properly. For SQL Server, preserves native connection strings as-is.
    /// </summary>
    public static string ResolveConnectionString(
        string? connectionString,
        DatabaseProviderType providerType,
        string defaultDbName = DatabasePathHelper.DefaultDatabaseFileName)
    {
        if (providerType == DatabaseProviderType.SqlServer)
        {
            return connectionString ?? string.Empty;
        }

        return DatabasePathHelper.ResolveConnectionString(connectionString, defaultDbName);
    }

    /// <summary>
    /// Configures DbContextOptionsBuilder with the appropriate database provider, resilience policies,
    /// and provider-specific migrations assembly folder context type.
    /// </summary>
    public static DbContextOptionsBuilder ConfigureAppDbContext(
        this DbContextOptionsBuilder options,
        string connectionString,
        DatabaseProviderType providerType,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
    {
        if (providerType == DatabaseProviderType.SqlServer)
        {
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                // Enable resilient execution strategy for Azure SQL Serverless auto-pause resumption and transient network drops
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                sqlServerOptionsAction?.Invoke(sqlServerOptions);
            });
        }
        else
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptionsAction?.Invoke(sqliteOptions);
            });
        }

        return options;
    }

    /// <summary>
    /// Registers <see cref="AppDbContext"/> with a provider-specific implementation so the correct
    /// EF Core migration set is discovered at runtime (Sqlite vs SqlServer).
    /// </summary>
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = DefaultConnectionStringName,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
    {
        var rawConnectionString = configuration.GetConnectionString(connectionStringName)
            ?? configuration[connectionStringName]
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionStringName}")
            ?? Environment.GetEnvironmentVariable(connectionStringName)
            ?? DatabasePathHelper.GetSqliteConnectionString();

        var providerType = DetermineProvider(configuration, rawConnectionString);
        var resolvedConnectionString = ResolveConnectionString(rawConnectionString, providerType);

        if (providerType == DatabaseProviderType.SqlServer)
        {
            services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options =>
            {
                options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction, sqliteOptionsAction);
            });
        }
        else
        {
            services.AddDbContext<AppDbContext, SqliteAppDbContext>(options =>
            {
                options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction, sqliteOptionsAction);
            });
        }

        return services;
    }

    /// <summary>
    /// Registers a provider-specific <see cref="AppDbContext"/> implementation for tests or custom hosts.
    /// </summary>
    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        string connectionString,
        DatabaseProviderType providerType,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
    {
        var resolvedConnectionString = ResolveConnectionString(connectionString, providerType);

        if (providerType == DatabaseProviderType.SqlServer)
        {
            services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options =>
            {
                options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction, sqliteOptionsAction);
            });
        }
        else
        {
            services.AddDbContext<AppDbContext, SqliteAppDbContext>(options =>
            {
                options.ConfigureAppDbContext(resolvedConnectionString, providerType, sqlServerOptionsAction, sqliteOptionsAction);
            });
        }

        return services;
    }
}
