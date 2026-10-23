using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using Xunit;

namespace PersonalFinance.Tests;

/// <summary>
/// Checks that an EF Core query translates to Azure SQL, without a database. Service tests run on the InMemory
/// provider, which evaluates any query in memory, so a query Azure SQL cannot run (e.g. <c>ToLowerInvariant()</c>
/// inside <c>Where</c>) passes there and fails in production. Build the query through the service's own
/// <c>IQueryable</c>-returning method against <see cref="CreateContext"/>, then call <see cref="AssertTranslates{T}"/>.
/// </summary>
public static class AzureSqlTranslation
{
    private const string OfflineConnectionString =
        "Server=localhost;Database=PersonalFinance_TranslationCheck;Trusted_Connection=True;TrustServerCertificate=True;";

    /// <summary>A context on the Azure SQL provider. It never connects: use it only to translate queries.</summary>
    public static AzureSqlAppDbContext CreateContext()
    {
        DbContextOptions<AzureSqlAppDbContext> options = new DbContextOptionsBuilder<AzureSqlAppDbContext>()
            .UseSqlServer(OfflineConnectionString)
            .Options;
        return new AzureSqlAppDbContext(options);
    }

    /// <summary>Translates <paramref name="query"/> to T-SQL and returns it; fails the test if it cannot.</summary>
    public static string AssertTranslates<T>(IQueryable<T> query)
    {
        try
        {
            return query.ToQueryString();
        }
        catch (InvalidOperationException ex)
        {
            Assert.Fail($"Query does not translate to Azure SQL: {ex.Message}");
            throw;
        }
    }
}
