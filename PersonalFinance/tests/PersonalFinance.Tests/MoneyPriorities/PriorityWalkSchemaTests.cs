using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The Priority Walk table as the Azure SQL migrations create it, checked against the spec's column table without a
/// database: the generated script is what production runs.
/// </summary>
public class PriorityWalkSchemaTests
{
    [Theory]
    [InlineData("[MonthlyAmount] decimal(18,2) NOT NULL")]
    [InlineData("[Purpose] nvarchar(300) NULL")]
    [InlineData("[DebtApr] decimal(5,2) NOT NULL")]
    [InlineData("[DebtBalance] decimal(18,2) NULL")]
    [InlineData("[EmergencyFundMonths] decimal(4,1) NULL")]
    [InlineData("[MonthlyExpenses] decimal(18,2) NULL")]
    [InlineData("[HasFullEmployerMatch] bit NULL")]
    [InlineData("[EmployerMatchPercent] decimal(5,2) NULL")]
    [InlineData("[HorizonYears] int NULL")]
    [InlineData("[AgeBand] nvarchar(16) NULL")]
    [InlineData("[AtContributionLimit] bit NULL")]
    [InlineData("[TreeVersion] int NOT NULL")]
    [InlineData("[LimitsTaxYear] int NULL")]
    [InlineData("[Path] nvarchar(200) NOT NULL")]
    [InlineData("[LeafId] nvarchar(8) NOT NULL")]
    [InlineData("[PurposeNote] nvarchar(2000) NULL")]
    [InlineData("[UserId] nvarchar(450) NOT NULL")]
    [InlineData("[IsDeleted] bit NOT NULL")]
    public void Migration_CreatesEveryColumnOfTheColumnTable(string column)
    {
        string createTable = CreateTableStatement();

        Assert.Contains(column, createTable, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_IndexesWalksByAccountAndCreatedDate()
    {
        Assert.Contains("CREATE INDEX [IX_PriorityWalks_UserId_CreatedAt] ON [PriorityWalks] ([UserId], [CreatedAt]);",
            MigrationScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void Model_HasNoChangesMissingFromTheMigrations()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        Assert.False(db.Database.HasPendingModelChanges(), "The model changed without a migration: add one.");
    }

    [Fact]
    public async Task SoftDeletedWalks_AreLeftOutOfQueries()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PriorityWalk_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options);
        var deleted = new PriorityWalk { UserId = "u", MonthlyAmount = 1m, TreeVersion = 1, Path = "S,Q1,L1", LeafId = "gone" };
        deleted.SoftDelete();
        db.PriorityWalks.AddRange(deleted,
            new PriorityWalk { UserId = "u", MonthlyAmount = 1m, TreeVersion = 1, Path = "S,Q1,L1", LeafId = "L1" });
        await db.SaveChangesAsync();

        List<string> leaves = await db.PriorityWalks.AsNoTracking().Select(w => w.LeafId).ToListAsync();

        Assert.Equal(["L1"], leaves);
    }

    /// <summary>
    /// The full Azure SQL migration script, generated once: it takes a few hundred milliseconds, and every column row
    /// reads the same script (#350).
    /// </summary>
    private static readonly Lazy<string> Script = new(() =>
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        return db.GetService<IMigrator>().GenerateScript();
    });

    private static string MigrationScript() => Script.Value;

    private static string CreateTableStatement()
    {
        string script = MigrationScript();
        int start = script.IndexOf("CREATE TABLE [PriorityWalks]", StringComparison.Ordinal);
        Assert.True(start >= 0, "No migration creates the PriorityWalks table.");
        int end = script.IndexOf(");", start, StringComparison.Ordinal);
        return script[start..end];
    }
}
