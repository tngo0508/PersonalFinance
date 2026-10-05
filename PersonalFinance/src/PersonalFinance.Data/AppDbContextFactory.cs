using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Data;

/// <summary>
/// Shared helpers for design-time EF Core factories (CLI migrations and database updates).
/// </summary>
internal static class DesignTimeDbContextFactoryHelper
{
    public static (DatabaseProviderType Provider, string ConnectionString) Resolve(string[]? args, DatabaseProviderType? forcedProvider = null)
    {
        string? rawConnectionString = null;
        string? explicitProvider = null;

        if (args != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--connection-string", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    rawConnectionString = args[i + 1];
                }
                else if (args[i].StartsWith("--connection-string=", StringComparison.OrdinalIgnoreCase))
                {
                    rawConnectionString = args[i].Substring("--connection-string=".Length);
                }
                else if (args[i].Equals("--provider", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    explicitProvider = args[i + 1];
                }
                else if (args[i].StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
                {
                    explicitProvider = args[i].Substring("--provider=".Length);
                }
            }
        }

        rawConnectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection");

        explicitProvider ??= Environment.GetEnvironmentVariable(DatabaseProviderHelper.DatabaseProviderEnvVar)
            ?? Environment.GetEnvironmentVariable("DB_PROVIDER");

        var providerType = forcedProvider
            ?? DatabaseProviderHelper.ParseProviderName(explicitProvider, rawConnectionString);

        if (providerType == DatabaseProviderType.Sqlite)
        {
            rawConnectionString ??= DatabasePathHelper.GetSqliteConnectionString();
        }
        else
        {
            rawConnectionString ??=
                "Server=localhost,1433;Database=PersonalFinance;User ID=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False";
        }

        var resolvedConnectionString = DatabaseProviderHelper.ResolveConnectionString(rawConnectionString, providerType);
        return (providerType, resolvedConnectionString);
    }
}

/// <summary>
/// Design-time factory for SQLite migrations (<c>Migrations/Sqlite</c>).
/// </summary>
public sealed class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var (_, connectionString) = DesignTimeDbContextFactoryHelper.Resolve(args, DatabaseProviderType.Sqlite);
        var optionsBuilder = new DbContextOptionsBuilder<SqliteAppDbContext>();
        optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.Sqlite);
        return new SqliteAppDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time factory for SQL Server / Azure SQL migrations (<c>Migrations/SqlServer</c>).
/// </summary>
public sealed class SqlServerAppDbContextFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args)
    {
        var (_, connectionString) = DesignTimeDbContextFactoryHelper.Resolve(args, DatabaseProviderType.SqlServer);
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerAppDbContext>();
        optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.SqlServer);
        return new SqlServerAppDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Back-compat design-time factory that resolves the provider from CLI args / env vars
/// and returns the matching provider-specific context as <see cref="AppDbContext"/>.
/// Prefer <see cref="SqliteAppDbContextFactory"/> or <see cref="SqlServerAppDbContextFactory"/> with <c>--context</c>.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var (providerType, connectionString) = DesignTimeDbContextFactoryHelper.Resolve(args);

        if (providerType == DatabaseProviderType.SqlServer)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqlServerAppDbContext>();
            optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.SqlServer);
            return new SqlServerAppDbContext(optionsBuilder.Options);
        }
        else
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqliteAppDbContext>();
            optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.Sqlite);
            return new SqliteAppDbContext(optionsBuilder.Options);
        }
    }
}
