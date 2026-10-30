using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Learning;
using PersonalFinance.ApiService.Features.Search;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Tests.Learning;

namespace PersonalFinance.Tests.Search;

/// <summary>
/// Term Entries in Global Search (#522), with the fixture content: a Learning group after the user's own results that
/// matches the Term, its aliases and its Vietnamese name, and nothing new for a query that names no Term.
/// </summary>
public class SearchLearningTests
{
    private const string UserId = "search-learning-user";

    [Fact]
    public async Task Abbreviation_FindsItsTermEntry()
    {
        await using AppDbContext db = CreateContext();

        SearchResultItemDto hit = Assert.Single((await CreateService(db).SearchAsync(UserId, "APR")).Learning);

        Assert.Equal(("apr", "Annual percentage rate", "/Learning/Term/apr", "Learning"),
            (hit.Id, hit.Title, hit.Url, hit.Source));
        Assert.Equal("The yearly cost of borrowing money, shown as a percent.", hit.Description);
    }

    [Theory]
    [InlineData("lãi kép", "compound-interest")]
    [InlineData("lai kep", "compound-interest")]
    [InlineData("diem tin dung", "credit-score")]
    public async Task VietnameseName_WithOrWithoutAccents_FindsItsTermEntry(string query, string slug)
    {
        await using AppDbContext db = CreateContext();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, query);

        Assert.Equal(slug, Assert.Single(results.Learning).Id);
    }

    [Fact]
    public async Task InVietnamese_TheHitIsNamedInVietnamese_AndTheEnglishTermStillMatches()
    {
        await using AppDbContext db = CreateContext();

        SearchResultItemDto hit = Assert.Single(
            (await CreateService(db).SearchAsync(UserId, "annual percentage", "vi")).Learning);

        Assert.Equal("Lãi suất phần trăm hằng năm", hit.Title);
    }

    [Fact]
    public async Task LearningGroup_ComesAfterTheUsersOwnResults()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(new SavingsGoal { UserId = UserId, Name = "Emergency fund top-up", TargetAmount = 1_000m });
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "emergency");

        Assert.Equal("Emergency fund top-up", Assert.Single(results.SavingsGoals).Title);
        Assert.Equal("emergency-fund", Assert.Single(results.Learning).Id);
        Assert.Equal(["Transactions", "Categories", "SavingsGoals", "Learning"],
            typeof(SearchResultsDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task QueryNamingNoTerm_AddsNothing()
    {
        await using AppDbContext db = CreateContext();
        db.SavingsGoals.Add(new SavingsGoal { UserId = UserId, Name = "Car", TargetAmount = 1_000m });
        await db.SaveChangesAsync();

        SearchResultsDto results = await CreateService(db).SearchAsync(UserId, "car");

        Assert.Single(results.SavingsGoals);
        Assert.Empty(results.Learning);
    }

    [Theory]
    [InlineData("Lãi Kép", "lai kep")]
    [InlineData("Điểm", "diem")]
    [InlineData("  ETF ", "etf")]
    public void Fold_IgnoresCaseAccentsAndSpaces(string text, string folded)
    {
        Assert.Equal(folded, SearchService.Fold(text));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SearchLearning_{Guid.NewGuid():N}")
            .Options);

    private static SearchService CreateService(AppDbContext db) =>
        new(db, new BudgetDataSourceService(db), TimeProvider.System,
            new LearningLibraryService(LearningContent.Load(LearningContentFolders.Fixture), TimeProvider.System));
}
