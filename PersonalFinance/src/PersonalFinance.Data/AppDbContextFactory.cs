using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Data;

/// <summary>
/// Design-time factory for EF Core tooling (CLI migrations and database updates).
/// Allows dotnet ef commands to run directly against the PersonalFinance.Data project.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(DatabasePathHelper.GetSqliteConnectionString());

        return new AppDbContext(optionsBuilder.Options);
    }
}
