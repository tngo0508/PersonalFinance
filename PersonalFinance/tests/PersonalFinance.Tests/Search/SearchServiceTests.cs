using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Search;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Shared.Enums;
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
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
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

    /// <summary>"Now" for every test: 21 October 2026, UTC.</summary>
    private static readonly TimeProvider Today = new FixedTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static SearchService CreateService(AppDbContext db) =>
        new(db, new BudgetDataSourceService(db), Today);

    private static BudgetCategory Category(string name, BudgetCategoryKind kind = BudgetCategoryKind.Expense,
        string userId = UserId) => new()
        {
            UserId = userId,
            Name = name,
            Kind = kind
        };

    private static BudgetTransaction TrackerTransaction(string description, DateOnly date, BudgetCategory category,
        string userId = UserId) => new()
        {
            UserId = userId,
            Date = date,
            Amount = 18.25m,
            Description = description,
            Category = category
        };

    private static UserBudgetSourcePreference Prefer(BudgetDataSource source) => new()
    {
        UserId = UserId,
        Source = source
    };

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_MatchesTransactionDescriptionAndLinksToTrackerMonth()
    {
        await using AppDbContext db = CreateContext();
        db.BudgetTransactions.Add(TrackerTransaction("Costco run", new DateOnly(2026, 8, 20), Category("Food")));
        await db.SaveChangesAsync();

        SearchResultItemDto result = Assert.Single((await CreateService(db).SearchAsync(UserId, "COSTCO")).Transactions);

        Assert.Equal("Costco run", result.Title);
        Assert.Equal("/BudgetTracker?year=2026&month=8", result.Url);
        Assert.Equal(new DateTime(2026, 8, 20), result.Date);
        Assert.Equal(18.25m, result.Amount);
        Assert.Contains("Food", result.Description);
        Assert.Contains("Expense", result.Description);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_MatchesTransactionsByCategoryName()
    {
        await using AppDbContext db = CreateContext();
        BudgetCategory groceries = Category("Groceries");
        db.BudgetTransactions.AddRange(
            TrackerTransaction("Weekly shop", new DateOnly(2026, 10, 4), groceries),
            TrackerTransaction("Bus pass", new DateOnly(2026, 10, 5), Category("Transport")));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "grocer");

        Assert.Equal("Weekly shop", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_DeletedCategoryStillNamesItsPastTransactions()
    {
        await using AppDbContext db = CreateContext();
        BudgetCategory deletedCategory = Category("Gym");
        deletedCategory.IsDeleted = true;
        db.BudgetTransactions.Add(TrackerTransaction("Monthly fee", new DateOnly(2026, 6, 1), deletedCategory));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "gym");

        Assert.Equal("Monthly fee", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_ExcludesDeletedTransactionsAndOtherAccounts()
    {
        await using AppDbContext db = CreateContext();
        BudgetTransaction deleted = TrackerTransaction("Costco deleted", new DateOnly(2026, 10, 2), Category("Food"));
        deleted.IsDeleted = true;
        db.BudgetTransactions.AddRange(
            TrackerTransaction("Costco mine", new DateOnly(2026, 10, 1), Category("Food")),
            TrackerTransaction("Costco theirs", new DateOnly(2026, 10, 1),
                Category("Food", userId: OtherUserId), OtherUserId),
            deleted);
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "costco");

        Assert.Equal("Costco mine", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_DefaultsToGoogleSheetWhenSyncedDataExists_AndNeverMixesSources()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco sheet", new DateTime(2026, 10, 1)));
        db.BudgetTransactions.Add(TrackerTransaction("Costco tracker", new DateOnly(2026, 10, 2), Category("Food")));
        await db.SaveChangesAsync();

        SearchResultItemDto result = Assert.Single((await CreateService(db).SearchAsync(UserId, "costco")).Transactions);

        Assert.Equal("Costco sheet", result.Title);
        Assert.StartsWith("/Analysis?", result.Url);
    }

    [Theory]
    [InlineData(BudgetDataSource.BudgetTracker, "Costco tracker")]
    [InlineData(BudgetDataSource.GoogleSheet, "Costco sheet")]
    public async Task SearchAsync_FollowsTheStoredDataSourcePreference(BudgetDataSource source, string expected)
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco sheet", new DateTime(2026, 10, 1)));
        db.BudgetTransactions.Add(TrackerTransaction("Costco tracker", new DateOnly(2026, 10, 2), Category("Food")));
        db.UserBudgetSourcePreferences.Add(Prefer(source));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "costco");

        Assert.Equal(expected, Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_SwitchingDataSource_ChangesResultsImmediately()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco sheet", new DateTime(2026, 10, 1)));
        db.BudgetTransactions.Add(TrackerTransaction("Costco tracker", new DateOnly(2026, 10, 2), Category("Food")));
        await db.SaveChangesAsync();
        var dataSource = new BudgetDataSourceService(db);
        var service = new SearchService(db, dataSource, Today);

        SearchResultsDto before = await service.SearchAsync(UserId, "costco");
        await dataSource.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker });
        SearchResultsDto after = await service.SearchAsync(UserId, "costco");

        Assert.Equal("Costco sheet", Assert.Single(before.Transactions).Title);
        Assert.Equal("Costco tracker", Assert.Single(after.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_FindsBudgetCategoriesByName()
    {
        await using AppDbContext db = CreateContext();
        db.BudgetCategories.AddRange(
            Category("Groceries"),
            Category("Salary", BudgetCategoryKind.Income),
            Category("Transport"));
        await db.SaveChangesAsync();

        SearchResultItemDto result = Assert.Single((await CreateService(db).SearchAsync(UserId, "GROCER")).Categories);

        Assert.Equal("Groceries", result.Title);
        Assert.Equal("Expense category", result.Description);
        Assert.Equal("/BudgetTracker?year=2026&month=10", result.Url);
        Assert.Equal("Income category",
            Assert.Single((await CreateService(db).SearchAsync(UserId, "salary")).Categories).Description);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_ExcludesDeletedCategoriesAndOtherAccounts()
    {
        await using AppDbContext db = CreateContext();
        BudgetCategory deleted = Category("Gym old");
        deleted.IsDeleted = true;
        db.BudgetCategories.AddRange(Category("Gym"), deleted, Category("Gym theirs", userId: OtherUserId));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "gym");

        Assert.Equal("Gym", Assert.Single(results.Categories).Title);
    }

    [Fact]
    public async Task SearchAsync_BudgetTrackerSource_ReturnsFiveCategoriesOrderedByName()
    {
        await using AppDbContext db = CreateContext();
        foreach (string? name in new[] { "Fun F", "Fun B", "Fun D", "Fun A", "Fun E", "Fun C" })
        {
            db.BudgetCategories.Add(Category(name));
        }

        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "fun");

        Assert.Equal(["Fun A", "Fun B", "Fun C", "Fun D", "Fun E"], results.Categories.Select(c => c.Title));
    }

    [Fact]
    public async Task SearchAsync_GoogleSheetSource_FindsDistinctSheetCategoryNames()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.AddRange(
            Transaction("Weekly shop", new DateTime(2026, 10, 1), "Groceries"),
            Transaction("Corner store", new DateTime(2026, 10, 2), "Groceries"),
            Transaction("Bus", new DateTime(2026, 10, 3), "Transport"),
            Transaction("Theirs", new DateTime(2026, 10, 3), "Groceries extra", OtherUserId));
        db.BudgetCategories.Add(Category("Groceries tracker"));
        await db.SaveChangesAsync();

        SearchResultItemDto result = Assert.Single((await CreateService(db).SearchAsync(UserId, "grocer")).Categories);

        Assert.Equal("Groceries", result.Title);
        Assert.Equal("Google Sheet category", result.Description);
        Assert.Equal("/Analysis?year=2026&month=10", result.Url);
    }

    [Fact]
    public async Task SearchAsync_DoesNotMatchBudgetPlannerPeriods()
    {
        await using AppDbContext db = CreateContext();
        db.UserBudgetAllocations.Add(new UserBudgetAllocation { UserId = UserId, Period = "2026-10" });
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "2026-10");

        Assert.Empty(results.Transactions);
        Assert.Empty(results.Categories);
        Assert.Empty(results.SavingsGoals);
    }

    [Theory]
    [InlineData(BudgetDataSource.BudgetTracker)]
    [InlineData(BudgetDataSource.GoogleSheet)]
    public async Task SearchAsync_SearchesSavingsGoalsForEitherDataSource(BudgetDataSource source)
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(Goal("Emergency fund"));
        db.UserBudgetSourcePreferences.Add(Prefer(source));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "emergency");

        Assert.Single(results.SavingsGoals);
    }

    [Theory]
    [InlineData("costco")]
    [InlineData("COSTCO")]
    [InlineData("whole")]
    public async Task SearchAsync_MatchesTransactionDescriptionCaseInsensitiveAnywhere(string query)
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco Wholesale", new DateTime(2026, 10, 3)));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, query);

        Assert.Equal("Costco Wholesale", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_MatchesTransactionCategory()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Weekly shop", new DateTime(2026, 10, 3), "Groceries"));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "grocer");

        Assert.Equal("Weekly shop", Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_TransactionResult_HasDateAmountAndLinkToItsMonthInAnalysis()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 9, 14)));
        await db.SaveChangesAsync();

        SearchResultItemDto result = Assert.Single((await CreateService(db).SearchAsync(UserId, "costco")).Transactions);

        Assert.Equal(new DateTime(2026, 9, 14), result.Date);
        Assert.Equal(42.50m, result.Amount);
        Assert.Equal("/Analysis?year=2026&month=9", result.Url);
        Assert.Contains("Food", result.Description);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestTenTransactions()
    {
        await using AppDbContext db = CreateContext();
        for (int day = 1; day <= 12; day++)
        {
            db.SpreadsheetTransactionRecords.Add(Transaction($"Coffee {day}", new DateTime(2026, 10, day)));
        }

        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "coffee");

        Assert.Equal(SearchService.MaxTransactions, results.Transactions.Count);
        Assert.Equal("Coffee 12", results.Transactions[0].Title);
        Assert.Equal("Coffee 3", results.Transactions[^1].Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesOtherAccountsAndSoftDeletedRecords()
    {
        await using AppDbContext db = CreateContext();
        SpreadsheetTransactionRecord deleted = Transaction("Costco deleted", new DateTime(2026, 10, 2));
        deleted.IsDeleted = true;
        SavingsGoal deletedGoal = Goal("Costco membership deleted");
        deletedGoal.IsDeleted = true;
        db.SpreadsheetTransactionRecords.AddRange(
            Transaction("Costco mine", new DateTime(2026, 10, 1)),
            Transaction("Costco theirs", new DateTime(2026, 10, 1), userId: OtherUserId),
            deleted);
        db.SavingsGoals.AddRange(Goal("Costco membership", userId: OtherUserId), deletedGoal);
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "costco");

        Assert.Equal("Costco mine", Assert.Single(results.Transactions).Title);
        Assert.Empty(results.SavingsGoals);
    }

    [Fact]
    public async Task SearchAsync_MatchesGoalNameOrDescription_OrderedByNameAndCapped()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.AddRange(
            Goal("Vacation"),
            Goal("Car", "Save for a vacation car"),
            Goal("House"),
            Goal("Vacation 2"), Goal("Vacation 3"), Goal("Vacation 4"), Goal("Vacation 5"));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "vacation");

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
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 10, 1)));
        db.SavingsGoals.Add(Goal("Car"));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, query!);

        Assert.Empty(results.Transactions);
        Assert.Empty(results.Categories);
        Assert.Empty(results.SavingsGoals);
    }

    [Theory]
    [InlineData("50%", "50% off sale", "50 off sale")]
    [InlineData("a_b", "Fee a_b", "Fee axb")]
    public async Task SearchAsync_WildcardCharactersMatchLiterally(string query, string literal, string wildcardOnly)
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.AddRange(
            Transaction(literal, new DateTime(2026, 10, 2)),
            Transaction(wildcardOnly, new DateTime(2026, 10, 1)));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, query);

        Assert.Equal(literal, Assert.Single(results.Transactions).Title);
    }

    [Fact]
    public async Task SearchAsync_TrimsQuery()
    {
        await using AppDbContext db = CreateContext();
        db.SpreadsheetTransactionRecords.Add(Transaction("Costco", new DateTime(2026, 10, 1)));
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "  costco  ");

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
        string? pattern = SearchService.ToContainsPattern(new string('a', SearchService.MaxQueryLength + 50));

        Assert.Equal($"%{new string('a', SearchService.MaxQueryLength)}%", pattern);
    }

    [Fact]
    public void AllSearchQueries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        SearchService service = CreateService(db);
        string pattern = SearchService.ToContainsPattern("50%_[x]")!;

        string[] queries = new[]
        {
            AzureSqlTranslation.AssertTranslates(service.SheetTransactionsQuery(UserId, pattern)),
            AzureSqlTranslation.AssertTranslates(service.TrackerTransactionsQuery(UserId, pattern)),
            AzureSqlTranslation.AssertTranslates(service.TrackerCategoriesQuery(UserId, pattern)),
            AzureSqlTranslation.AssertTranslates(service.SheetCategoriesQuery(UserId, pattern)),
            AzureSqlTranslation.AssertTranslates(service.SavingsGoalsQuery(UserId, pattern))
        };

        Assert.All(queries, sql =>
        {
            Assert.Contains("LIKE", sql, StringComparison.Ordinal);
            Assert.Contains("ESCAPE", sql, StringComparison.Ordinal);
        });
    }
}
