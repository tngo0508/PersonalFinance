using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Analysis;
using PersonalFinance.ApiService.Features.BudgetTracker;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Data Source Removal (ADR 0002 section 4): the per-source counts, the soft deletes of each source, what is left
/// alone, and how the stored preference follows.
/// </summary>
public class DataSourceRemovalTests
{
    private const string UserId = "removal-user";
    private const string OtherUserId = "removal-other";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DataSourceRemoval_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Two synced months (one with two snapshots) and three synced transactions.</summary>
    private static GoogleDriveConnection SeedSheet(AppDbContext db, string userId)
    {
        var connection = new GoogleDriveConnection
            { UserId = userId, Name = "Budget", FolderId = "f", FolderUrl = "u" };

        SpreadsheetBudgetSnapshot Snapshot(int month) => new()
        {
            UserId = userId, Connection = connection, FileId = "file", FileName = "budget.xlsx", Year = 2026,
            MonthNumber = month, CategoryBreakdownJson = "[]"
        };

        SpreadsheetBudgetSnapshot march = Snapshot(3);
        db.SpreadsheetBudgetSnapshots.AddRange(march, Snapshot(3), Snapshot(4));
        for (int i = 0; i < 3; i++)
        {
            db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
            {
                UserId = userId, Snapshot = march, Connection = connection, Description = $"rent {i}",
                Date = new DateTime(2026, 3, 1)
            });
        }

        return connection;
    }

    /// <summary>A plan for March, and transactions in March and May: two months, two transactions.</summary>
    private static void SeedTracker(AppDbContext db, string userId)
    {
        var category = new BudgetCategory { UserId = userId, Name = "Groceries", Kind = BudgetCategoryKind.Expense };
        var plan = new MonthlyBudgetPlan { UserId = userId, Year = 2026, Month = 3 };
        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine
            { UserId = userId, Plan = plan, Category = category, PlannedAmount = 400m });
        db.BudgetTransactions.AddRange(
            new BudgetTransaction
            {
                UserId = userId, Category = category, Date = new DateOnly(2026, 3, 9), Amount = 52.10m,
                Description = "market"
            },
            new BudgetTransaction
            {
                UserId = userId, Category = category, Date = new DateOnly(2026, 5, 2), Amount = 18.75m,
                Description = "bakery"
            });
    }

    private static void SeedPreference(AppDbContext db, string userId, BudgetDataSource source) =>
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference { UserId = userId, Source = source });

    private static GoogleSheetRemovalService SheetRemoval(AppDbContext db, BudgetDataSourceService sources) =>
        new(db, sources, new FixedTimeProvider(Now));

    private static BudgetTrackerRemovalService TrackerRemoval(AppDbContext db, BudgetDataSourceService sources) =>
        new(db, sources, new FixedTimeProvider(Now));

    [Fact]
    public async Task GetDataSummaries_CountsDistinctMonthsAndTransactions_PerSource_ForThisUserOnly()
    {
        await using AppDbContext db = CreateContext();
        SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedSheet(db, OtherUserId);
        SeedTracker(db, OtherUserId);
        await db.SaveChangesAsync();

        List<BudgetDataSourceSummaryDto>
            summaries = await new BudgetDataSourceService(db).GetDataSummariesAsync(UserId);

        Assert.Collection(summaries,
            sheet =>
            {
                Assert.Equal(BudgetDataSource.GoogleSheet, sheet.Source);
                Assert.Equal(2, sheet.MonthCount);
                Assert.Equal(3, sheet.TransactionCount);
                Assert.True(sheet.HasData);
            },
            tracker =>
            {
                Assert.Equal(BudgetDataSource.BudgetTracker, tracker.Source);
                Assert.Equal(2, tracker.MonthCount);
                Assert.Equal(2, tracker.TransactionCount);
                Assert.True(tracker.HasData);
            });
    }

    [Fact]
    public async Task GetDataSummaries_ForANewUser_ReportsNoData()
    {
        await using AppDbContext db = CreateContext();

        List<BudgetDataSourceSummaryDto>
            summaries = await new BudgetDataSourceService(db).GetDataSummariesAsync(UserId);

        Assert.All(summaries, s => Assert.False(s.HasData));
    }

    [Fact]
    public async Task RemoveGoogleSheet_SoftDeletesSyncedData_KeepsDriveConnectionAndTracker_AndOtherUsers()
    {
        await using AppDbContext db = CreateContext();
        GoogleDriveConnection connection = SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedSheet(db, OtherUserId);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);

        BudgetDataSourceSummaryDto removed = await SheetRemoval(db, sources).RemoveAsync(UserId);

        Assert.Equal(2, removed.MonthCount);
        Assert.Equal(3, removed.TransactionCount);
        Assert.False(await db.SpreadsheetBudgetSnapshots.AnyAsync(s => s.UserId == UserId));
        Assert.False(await db.SpreadsheetTransactionRecords.AnyAsync(t => t.UserId == UserId));
        Assert.All(
            await db.SpreadsheetBudgetSnapshots.IgnoreQueryFilters().Where(s => s.UserId == UserId).ToListAsync(),
            s => Assert.Equal(Now.UtcDateTime, s.DeletedAt));
        Assert.True(await db.GoogleDriveConnections.AnyAsync(c => c.Id == connection.Id));
        Assert.Equal(2, await db.BudgetTransactions.CountAsync(t => t.UserId == UserId));
        Assert.Equal(3, await db.SpreadsheetBudgetSnapshots.CountAsync(s => s.UserId == OtherUserId));
    }

    [Fact]
    public async Task RemoveGoogleSheet_WhenPreferenceIsGoogleSheet_ClearsIt_SoTheDefaultPicksTheTracker()
    {
        await using AppDbContext db = CreateContext();
        SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedPreference(db, UserId, BudgetDataSource.GoogleSheet);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);
        int changes = 0;
        sources.PreferenceChanged += _ => changes++;

        await SheetRemoval(db, sources).RemoveAsync(UserId);

        BudgetDataSourceDto active = await sources.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.BudgetTracker, active.Source);
        Assert.True(active.IsDefault);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task RemoveBudgetTracker_WhenPreferenceIsBudgetTracker_ClearsIt_SoTheDefaultPicksTheSheet()
    {
        await using AppDbContext db = CreateContext();
        SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedPreference(db, UserId, BudgetDataSource.BudgetTracker);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);

        await TrackerRemoval(db, sources).RemoveAsync(UserId);

        BudgetDataSourceDto active = await sources.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.GoogleSheet, active.Source);
        Assert.True(active.IsDefault);
    }

    [Fact]
    public async Task RemovingTheOtherSource_KeepsTheStoredPreference_ButStillSignalsAChange()
    {
        await using AppDbContext db = CreateContext();
        SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedPreference(db, UserId, BudgetDataSource.BudgetTracker);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);
        int changes = 0;
        sources.PreferenceChanged += _ => changes++;

        await SheetRemoval(db, sources).RemoveAsync(UserId);

        BudgetDataSourceDto active = await sources.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.BudgetTracker, active.Source);
        Assert.False(active.IsDefault);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task SettingASourceAfterItsPreferenceWasCleared_StoresANewPreference()
    {
        await using AppDbContext db = CreateContext();
        SeedTracker(db, UserId);
        SeedPreference(db, UserId, BudgetDataSource.BudgetTracker);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);
        await TrackerRemoval(db, sources).RemoveAsync(UserId);

        await sources.SetDataSourcePreferenceAsync(UserId,
            new SetBudgetDataSourceRequest { Source = BudgetDataSource.GoogleSheet });

        BudgetDataSourceDto active = await sources.GetDataSourcePreferenceAsync(UserId);
        Assert.Equal(BudgetDataSource.GoogleSheet, active.Source);
        Assert.False(active.IsDefault);
    }

    [Fact]
    public async Task RemoveBudgetTracker_SoftDeletesEverything_KeepsSheetAndOtherUsers()
    {
        await using AppDbContext db = CreateContext();
        SeedSheet(db, UserId);
        SeedTracker(db, UserId);
        SeedTracker(db, OtherUserId);
        await db.SaveChangesAsync();
        var sources = new BudgetDataSourceService(db);

        BudgetDataSourceSummaryDto removed = await TrackerRemoval(db, sources).RemoveAsync(UserId);

        Assert.Equal(2, removed.MonthCount);
        Assert.Equal(2, removed.TransactionCount);
        Assert.False(await db.BudgetTransactions.AnyAsync(t => t.UserId == UserId));
        Assert.False(await db.MonthlyBudgetPlans.AnyAsync(p => p.UserId == UserId));
        Assert.False(await db.MonthlyBudgetLines.AnyAsync(l => l.UserId == UserId));
        Assert.False(await db.BudgetCategories.AnyAsync(c => c.UserId == UserId));
        Assert.All(await db.BudgetTransactions.IgnoreQueryFilters().Where(t => t.UserId == UserId).ToListAsync(),
            t => Assert.Equal(Now.UtcDateTime, t.DeletedAt));
        Assert.Equal(3, await db.SpreadsheetBudgetSnapshots.CountAsync(s => s.UserId == UserId));
        Assert.Equal(2, await db.BudgetTransactions.CountAsync(t => t.UserId == OtherUserId));
    }

    [Fact]
    public async Task AfterRemovingTheBudgetTracker_OpeningItSeedsTheDefaultCategoriesAgain()
    {
        await using AppDbContext db = CreateContext();
        var budget = new MonthlyBudgetService(db);
        await budget.EnsureDefaultCategoriesAsync(UserId);
        await budget.CreateCategoryAsync(UserId,
            new SaveCategoryRequest { Name = "Hobbies", Kind = BudgetCategoryKind.Expense, SortOrder = 5 });

        await TrackerRemoval(db, new BudgetDataSourceService(db)).RemoveAsync(UserId);
        List<BudgetCategoryDto> categories = await budget.GetCategoriesAsync(UserId);

        Assert.Equal(16, categories.Count);
        Assert.DoesNotContain(categories, c => c.Name == "Hobbies");
    }

    [Fact]
    public async Task ArchivingEveryCategory_WhileTransactionsRemain_DoesNotSeedTheDefaults()
    {
        await using AppDbContext db = CreateContext();
        SeedTracker(db, UserId);
        await db.SaveChangesAsync();
        var budget = new MonthlyBudgetService(db);
        BudgetCategory groceries = await db.BudgetCategories.SingleAsync(c => c.UserId == UserId);
        await budget.DeleteCategoryAsync(UserId, groceries.Id);

        await budget.EnsureDefaultCategoriesAsync(UserId);

        Assert.False(await db.BudgetCategories.AnyAsync(c => c.UserId == UserId));
    }

    [Fact]
    public void MonthQueries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        var sources = new BudgetDataSourceService(db);

        AzureSqlTranslation.AssertTranslates(sources.SheetMonthsQuery(UserId));
        AzureSqlTranslation.AssertTranslates(sources.TrackerMonthsQuery(UserId));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
