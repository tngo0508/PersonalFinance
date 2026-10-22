using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Data;

/// <summary>
/// Shared helpers for design-time EF Core factories (CLI migrations and database updates).
/// </summary>
internal static class DesignTimeDbContextFactoryHelper
{
    public static string ResolveConnectionString(string[]? args)
    {
        string? rawConnectionString = null;

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
            }
        }

        rawConnectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection")
            ?? DatabaseProviderHelper.DefaultSqlConnectionString;

        return DatabaseProviderHelper.ResolveConnectionString(rawConnectionString);
    }
}

/// <summary>
/// Design-time factory for the SQL Server migrations in <c>Migrations/AzureSql</c>, which serve both
/// Azure SQL and local SQL Server.
/// </summary>
public sealed class AzureSqlAppDbContextFactory : IDesignTimeDbContextFactory<AzureSqlAppDbContext>
{
    public AzureSqlAppDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeDbContextFactoryHelper.ResolveConnectionString(args);
        var optionsBuilder = new DbContextOptionsBuilder<AzureSqlAppDbContext>();
        optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.AzureSql);
        return new AzureSqlAppDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time factory for <see cref="AppDbContext"/>.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeDbContextFactoryHelper.ResolveConnectionString(args);
        var optionsBuilder = new DbContextOptionsBuilder<AzureSqlAppDbContext>();
        optionsBuilder.ConfigureAppDbContext(connectionString, DatabaseProviderType.AzureSql);
        return new AzureSqlAppDbContext(optionsBuilder.Options);
    }
}
