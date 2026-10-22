using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using Xunit;

namespace PersonalFinance.Tests.Search;

/// <summary>
/// Global Search behaviour through <see cref="SearchService.SearchAsync"/>, plus a guard that every search query
/// translates to Azure SQL. The in-memory provider runs filters in memory, so it alone cannot catch untranslatable
/// queries.
/// </summary>
public class SearchServiceTests
{
    private const string UserId = "search-user";
    private const string OtherUserId = "other-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Search_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static SpreadsheetTransactionRecord Transaction(string description, DateTime date,
        string category = "Food", string userId = UserId) => new()
    {
        UserId = userId,
        Date = date,
        Amount = 42.50m,
        Description = description,
        Category = category,
        Type = "Expense"
    };

    private static SavingsGoal Goal(string name, string? description = null, string userId = UserId) => new()
    {
        UserId = userId,
        Name = name,
        Description = description,
        TargetAmount = 1_000m
    };

    [Theory]
    [InlineData("costco")]
    [InlineData("COSTCO")]
    [InlineData("whole")]
    public async Task SearchAsync_MatchesTransactionDescriptionCaseInsensitiveAnywhere(string query)
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco Wholesale", new DateTime(2026, 10, 3)));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, query);

        Assert.Equal("Costco Wholesale", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_MatchesTransactionCategory()
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Weekly shop", new DateTime(2026, 10, 3), "Groceries"));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, "grocer");

        Assert.Equal("Weekly shop", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_TransactionResult_HasDateAmountAndLinkToItsMonthInAnalysis()
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 9, 14)));
        await db.SaveChangesAsync();

        var result = Assert.Single((await new SearchService(db).SearchAsync(UserId, "costco")).Transactions);

        Assert.Equal(new DateTime(2026, 9, 14), result.Date);
        Assert.Equal(42.50m, result.Amount);
        Assert.Equal("/Analysis?year=2026&month=9", result.Url);
        Assert.Contains("Food", result.Description);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestTenTransactions()
    {
        await using var db = CreateContext();
        for (var day = 1; day <= 12; day++)
        {
            db.SpreadsheetTransactionRecords.Add(Transaction($"Coffee {day}", new DateTime(2026, 10, day)));
        }

        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, "coffee");

        Assert.Equal(SearchService.MaxTransactions, results.Transactions.Count);
        Assert.Equal("Coffee 12", results.Transactions[0].Title);
        Assert.Equal("Coffee 3", results.Transactions[^1].Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesOtherAccountsAndSoftDeletedRecords()
    {
        await using var db = CreateContext();
        var deleted = Transaction("Costco deleted", new DateTime(2026, 10, 2));
        deleted.IsDeleted = true;
        var deletedGoal = Goal("Costco membership deleted");
        deletedGoal.IsDeleted = true;
        db.SpreadsheetTransactionRecords.AddRange(
            Transaction("Costco mine", new DateTime(2026, 10, 1)),
            Transaction("Costco theirs", new DateTime(2026, 10, 1), userId: OtherUserId),
            deleted);
        db.SavingsGoals.AddRange(Goal("Costco membership", userId: OtherUserId), deletedGoal);
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, "costco");

        Assert.Equal("Costco mine", Assert.Single(results.Transactions).Title);
        Assert.Empty(results.SavingsGoals);
    }

    [Fact]
    public async Task SearchAsync_MatchesGoalNameOrDescription_OrderedByNameAndCapped()
    {
        await using var db = CreateContext();
        db.SavingsGoals.AddRange(
            Goal("Vacation"),
            Goal("Car", "Save for a vacation car"),
            Goal("House"),
            Goal("Vacation 2"), Goal("Vacation 3"), Goal("Vacation 4"), Goal("Vacation 5"));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, "vacation");

        Assert.Equal(SearchService.MaxSavingsGoals, results.SavingsGoals.Count);
        Assert.Equal(["Car", "Vacation", "Vacation 2", "Vacation 3", "Vacation 4"],
            results.SavingsGoals.Select(g => g.Title));
        Assert.All(results.SavingsGoals, g => Assert.Equal("/SavingsGoals", g.Url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" c ")]
    [InlineData(null)]
    public async Task SearchAsync_QueryShorterThanTwoCharacters_ReturnsNothing(string? query)
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 10, 1)));
        db.SavingsGoals.Add(Goal("Car"));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, query!);

        Assert.Empty(results.Transactions);
        Assert.Empty(results.BudgetItems);
        Assert.Empty(results.SavingsGoals);
    }

    [Theory]
    [InlineData("50%", "50% off sale", "50 off sale")]
    [InlineData("a_b", "Fee a_b", "Fee axb")]
    public async Task SearchAsync_WildcardCharactersMatchLiterally(string query, string literal, string wildcardOnly)
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.AddRange(
            Transaction(literal, new DateTime(2026, 10, 2)),
            Transaction(wildcardOnly, new DateTime(2026, 10, 1)));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, query);

        Assert.Equal(literal, Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_TrimsQuery()
    {
        await using var db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 10, 1)));
        await db.SaveChangesAsync();

        var results = await new SearchService(db).SearchAsync(UserId, "  costco  ");

        Assert.Single(results.Transactions);
    }

    [Theory]
    [InlineData("50%", @"%50\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData("[x]", @"%\[x]%")]
    [InlineData(@"c:\d", @"%c:\\d%")]
    public void ToContainsPattern_EscapesLikeWildcards(string query, string expected)
    {
        // "[" is a wildcard on Azure SQL only; the in-memory provider's LIKE ignores the escape for it, so the
        // escaping is checked on the pattern itself.
        Assert.Equal(expected, SearchService.ToContainsPattern(query));
    }

    [Fact]
    public void ToContainsPattern_CutsLongQueriesToMaxLength()
    {
        var pattern = SearchService.ToContainsPattern(new string('a', SearchService.MaxQueryLength + 50));

        Assert.Equal($"%{new string('a', SearchService.MaxQueryLength)}%", pattern);
    }

    [Fact]
    public void AllSearchQueries_TranslateToAzureSql()
    {
        // Translate only; no database is contacted. Same approach as the Azure SQL migration-script tests.
        var options = new DbContextOptionsBuilder<AzureSqlAppDbContext>()
            .UseSqlServer("Server=localhost;Database=PersonalFinance_SearchCheck;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var db = new AzureSqlAppDbContext(options);
        var service = new SearchService(db);
        var pattern = SearchService.ToContainsPattern("50%_[x]")!;

        var queries = new[]
        {
            service.TransactionsQuery(UserId, pattern).ToQueryString(),
            service.BudgetItemsQuery(UserId, pattern).ToQueryString(),
            service.SavingsGoalsQuery(UserId, pattern).ToQueryString()
        };

        Assert.All(queries, sql =>
        {
            Assert.Contains("LIKE", sql, StringComparison.Ordinal);
            Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        });
    }
}
