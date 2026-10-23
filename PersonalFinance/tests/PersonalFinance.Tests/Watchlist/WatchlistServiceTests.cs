using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Watchlist;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.Tests.Watchlist;

/// <summary>
/// The Account's Watchlist of saved articles, against an in-memory database: saving, listing, removing (a soft
/// delete) and checking, each scoped to the signed-in Account. The in-memory provider does not enforce unique indexes,
/// so the tests count rows to catch what Azure SQL would reject.
/// </summary>
public class WatchlistServiceTests
{
    private const string UserId = "watchlist-user";
    private const string OtherUserId = "other-user";
    private const string Url = "https://example.test/rates-fall";

    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Watchlist_{Guid.NewGuid():N}")
            .Options);

    private WatchlistService CreateService(AppDbContext db) =>
        new(db, _clock, NullLogger<WatchlistService>.Instance);

    private static SaveArticleRequestDto Article(string url = Url, string title = "Rates fall", string? notes = null) =>
        new() { Title = title, Url = url, Source = "BBC Business", Category = "General", Notes = notes };

    private static Task<int> RowsFor(AppDbContext db, string userId, string url) =>
        db.SavedArticles.IgnoreQueryFilters().CountAsync(a => a.UserId == userId && a.Url == url);

    [Fact]
    public void SavedArticles_AreUniquePerAccountAndUrl_IncludingRemovedOnes()
    {
        using AppDbContext db = CreateContext();

        IIndex index = Assert.Single(db.Model.FindEntityType(typeof(SavedArticle))!.GetIndexes(), i => i.IsUnique);

        Assert.Equal([nameof(SavedArticle.UserId), nameof(SavedArticle.Url)], index.Properties.Select(p => p.Name));
        Assert.Null(index.GetFilter());
    }

    [Fact]
    public async Task Save_StoresTheArticleForTheAccount_AtTheCurrentTime()
    {
        await using AppDbContext db = CreateContext();

        SaveArticleResultDto result = await CreateService(db).SaveArticleAsync(UserId, Article(notes: "Read later"));

        Assert.True(result.Success);
        Assert.False(result.AlreadySaved);
        SavedArticle saved = await db.SavedArticles.SingleAsync();
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(("Rates fall", Url, "BBC Business", "General", "Read later"),
            (saved.Title, saved.Url, saved.Source, saved.Category, saved.Notes));
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, saved.SavedAtUtc);
    }

    [Fact]
    public async Task Save_TheSameUrlAgain_ReturnsAlreadySaved_KeepsOneRow_AndUpdatesTheNotes()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        SaveArticleResultDto first = await service.SaveArticleAsync(UserId, Article(notes: "First"));

        SaveArticleResultDto again = await service.SaveArticleAsync(UserId, Article(notes: "Second"));

        Assert.True(again.AlreadySaved);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await RowsFor(db, UserId, Url));
        Assert.Equal("Second", (await db.SavedArticles.SingleAsync()).Notes);
    }

    [Fact]
    public async Task Save_AfterRemoving_RestoresTheRemovedRow_InsteadOfAddingADuplicate()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        SaveArticleResultDto first = await service.SaveArticleAsync(UserId, Article(title: "Old title"));
        await service.RemoveArticleAsync(UserId, first.Id);
        _clock.Advance(TimeSpan.FromHours(1));

        SaveArticleResultDto again = await service.SaveArticleAsync(UserId, Article(title: "New title"));

        Assert.False(again.AlreadySaved);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await RowsFor(db, UserId, Url));
        SavedArticle restored = await db.SavedArticles.SingleAsync();
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Equal("New title", restored.Title);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, restored.SavedAtUtc);
    }

    [Fact]
    public async Task GetWatchlist_ReturnsOnlyTheAccountsArticles_NewestFirst_LeavingOutRemovedOnes()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        await service.SaveArticleAsync(UserId, Article("https://example.test/a", "Older"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.SaveArticleAsync(UserId, Article("https://example.test/b", "Newer"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        SaveArticleResultDto removed = await service.SaveArticleAsync(UserId, Article("https://example.test/c", "Removed"));
        await service.RemoveArticleAsync(UserId, removed.Id);
        await service.SaveArticleAsync(OtherUserId, Article("https://example.test/d", "Someone else's"));

        List<SavedArticleResponseDto> watchlist = await service.GetWatchlistAsync(UserId);

        Assert.Equal(["Newer", "Older"], watchlist.Select(a => a.Title));
    }

    [Fact]
    public async Task Remove_SoftDeletesTheAccountsOwnArticle()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        SaveArticleResultDto saved = await service.SaveArticleAsync(UserId, Article());

        bool removed = await service.RemoveArticleAsync(UserId, saved.Id);

        Assert.True(removed);
        SavedArticle row = await db.SavedArticles.IgnoreQueryFilters().SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
        Assert.Empty(await db.SavedArticles.ToListAsync());
    }

    [Fact]
    public async Task Remove_AnotherAccountsArticle_ReturnsFalse_AndLeavesItSaved()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        SaveArticleResultDto theirs = await service.SaveArticleAsync(OtherUserId, Article());

        bool removed = await service.RemoveArticleAsync(UserId, theirs.Id);

        Assert.False(removed);
        Assert.False((await db.SavedArticles.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Remove_AnUnknownId_ReturnsFalse()
    {
        await using AppDbContext db = CreateContext();

        Assert.False(await CreateService(db).RemoveArticleAsync(UserId, 404));
    }

    [Fact]
    public async Task IsArticleSaved_MatchesOnlyTheAccountsOwnSavedUrl_AndNotAfterRemoval()
    {
        await using AppDbContext db = CreateContext();
        WatchlistService service = CreateService(db);
        SaveArticleResultDto saved = await service.SaveArticleAsync(UserId, Article());

        Assert.True(await service.IsArticleSavedAsync(UserId, Url));
        Assert.False(await service.IsArticleSavedAsync(OtherUserId, Url));
        Assert.False(await service.IsArticleSavedAsync(UserId, "https://example.test/other"));

        await service.RemoveArticleAsync(UserId, saved.Id);

        Assert.False(await service.IsArticleSavedAsync(UserId, Url));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
