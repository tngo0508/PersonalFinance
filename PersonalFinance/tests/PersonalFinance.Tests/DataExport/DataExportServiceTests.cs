using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests.DataExport;

/// <summary>
/// Behaviour of <see cref="IDataExportService"/> against an in-memory database: an Account gets its own live
/// records, never another Account's, never soft-deleted rows and never secrets.
/// </summary>
public class DataExportServiceTests
{
    private const string UserId = "export-user";
    private const string OtherUserId = "other-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DataExport_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static DataExportService CreateService(AppDbContext db) => new(db, new FixedTimeProvider(Now));

    [Fact]
    public async Task Export_ContainsTheAccountAndItsRecords()
    {
        await using var db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId, UserName = "me@example.com", Email = "me@example.com" });
        db.Roles.Add(new IdentityRole { Id = "role-user", Name = "User" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = UserId, RoleId = "role-user" });
        db.UserLogins.Add(new IdentityUserLogin<string>
            { UserId = UserId, LoginProvider = "Google", ProviderKey = "g-1", ProviderDisplayName = "Google" });
        db.UserConsents.Add(new UserConsent
        {
            UserId = UserId, PolicyVersion = "1.0", AcceptedTerms = true, AcceptedPrivacy = true,
            IpAddress = "203.0.113.7", CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)
        });
        db.SavingsGoals.Add(new SavingsGoal { UserId = UserId, Name = "Emergency fund", TargetAmount = 10_000m });
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal(Now.UtcDateTime, export.ExportedAtUtc);
        Assert.Equal("me@example.com", export.Account.Email);
        Assert.Equal(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), export.Account.RegisteredAtUtc);
        Assert.Equal(["User"], export.Account.Roles);
        Assert.Equal(["Google"], export.Account.ExternalLogins);
        Assert.Equal("203.0.113.7", Assert.Single(export.Consents).IpAddress);
        Assert.Equal("Emergency fund", Assert.Single(export.SavingsGoals).Name);
    }

    [Fact]
    public async Task Export_NeverContainsAnotherAccountsRecords()
    {
        await using var db = CreateContext();
        SeedOneOfEverything(db, UserId, "mine");
        SeedOneOfEverything(db, OtherUserId, "theirs");
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        var json = JsonSerializer.Serialize(export);
        Assert.Contains("mine", json);
        Assert.DoesNotContain("theirs", json);
    }

    [Fact]
    public async Task Export_CoversEverySection()
    {
        await using var db = CreateContext();
        SeedOneOfEverything(db, UserId, "mine");
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        Assert.Single(export.NetWorthParameters);
        Assert.Single(export.BudgetAllocations);
        Assert.Equal("BudgetTracker", export.BudgetDataSource);
        Assert.Equal("Expense", Assert.Single(export.BudgetCategories).Kind);
        Assert.Single(export.BudgetMonthLines);
        Assert.Single(export.BudgetTransactions);
        Assert.Single(export.GoogleDriveConnections);
        Assert.Single(export.SpreadsheetMonths);
        Assert.Single(export.SpreadsheetTransactions);
        Assert.Single(export.SavingsGoals);
        Assert.Single(export.SavedArticles);
        Assert.Single(export.Community.Stories);
        Assert.Single(export.Community.Comments);
        Assert.Single(export.Community.StoryLikes);
        Assert.Single(export.Community.CommentLikes);
        Assert.Equal("Yes", Assert.Single(export.Community.PollVotes).Answer);
        Assert.Equal("Idea", Assert.Single(export.Feedback).Category);
        Assert.Single(export.Notifications);
        Assert.Single(export.AiUsage);
    }

    [Fact]
    public async Task Export_LeavesOutSoftDeletedRows()
    {
        await using var db = CreateContext();
        var live = new SavingsGoal { UserId = UserId, Name = "Live goal" };
        var deleted = new SavingsGoal { UserId = UserId, Name = "Deleted goal" };
        deleted.SoftDelete();
        db.SavingsGoals.AddRange(live, deleted);
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal("Live goal", Assert.Single(export.SavingsGoals).Name);
    }

    [Fact]
    public async Task Export_TransactionInADeletedCategory_KeepsTheCategoryName()
    {
        await using var db = CreateContext();
        var category = new BudgetCategory { UserId = UserId, Name = "Old category", Kind = BudgetCategoryKind.Expense };
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();
        db.BudgetTransactions.Add(new BudgetTransaction
            { UserId = UserId, CategoryId = category.Id, Date = new DateOnly(2026, 3, 1), Amount = 12.34m });
        category.SoftDelete();
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        Assert.Empty(export.BudgetCategories);
        var transaction = Assert.Single(export.BudgetTransactions);
        Assert.Equal("Old category", transaction.Category);
        Assert.Equal(12.34m, transaction.Amount);
    }

    [Fact]
    public async Task Export_NeverContainsGoogleDriveApiKeys()
    {
        await using var db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            UserId = UserId, Name = "Budget folder", FolderId = "folder", FolderUrl = "https://drive.example/folder",
            EncryptedApiKey = "encrypted-secret-key", MaskedApiKey = "AIza****cret"
        });
        await db.SaveChangesAsync();

        var export = await CreateService(db).ExportAsync(UserId);

        var json = JsonSerializer.Serialize(export);
        Assert.Contains("Budget folder", json);
        Assert.DoesNotContain("encrypted-secret-key", json);
        Assert.DoesNotContain("AIza", json);
    }

    [Fact]
    public async Task Export_UnknownAccount_ReturnsEmptySections()
    {
        await using var db = CreateContext();

        var export = await CreateService(db).ExportAsync("nobody");

        Assert.Equal("nobody", export.Account.Id);
        Assert.Null(export.Account.Email);
        Assert.Null(export.Account.RegisteredAtUtc);
        Assert.Null(export.BudgetDataSource);
        Assert.Empty(export.BudgetTransactions);
        Assert.Empty(export.Community.Stories);
    }

    [Fact]
    public void JoinQueries_TranslateToAzureSql()
    {
        using var db = AzureSqlTranslation.CreateContext();
        var service = CreateService(db);

        AzureSqlTranslation.AssertTranslates(service.BudgetTransactionsQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.BudgetMonthLinesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.StoryLikesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.PollVotesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.AccountRolesQuery(UserId));
    }

    /// <summary>
    /// One record in every section for <paramref name="userId"/>, each with <paramref name="marker"/> in a text
    /// field, so a serialized export shows whose records it holds.
    /// </summary>
    private static void SeedOneOfEverything(AppDbContext db, string userId, string marker)
    {
        db.Users.Add(new IdentityUser { Id = userId, UserName = $"{marker}@example.com", Email = $"{marker}@example.com" });
        db.UserConsents.Add(new UserConsent { UserId = userId, PolicyVersion = marker });
        db.NetWorthParameters.Add(new NetWorthParameters { UserId = userId, StartingBalance = 1000m });
        db.UserBudgetAllocations.Add(new UserBudgetAllocation { UserId = userId, Period = marker });
        db.UserBudgetSourcePreferences.Add(new UserBudgetSourcePreference { UserId = userId, Source = BudgetDataSource.BudgetTracker });

        var category = new BudgetCategory { UserId = userId, Name = $"{marker} category", Kind = BudgetCategoryKind.Expense };
        var plan = new MonthlyBudgetPlan { UserId = userId, Year = 2026, Month = 3 };
        db.MonthlyBudgetLines.Add(new MonthlyBudgetLine { UserId = userId, Plan = plan, Category = category, PlannedAmount = 50m });
        db.BudgetTransactions.Add(new BudgetTransaction
            { UserId = userId, Category = category, Date = new DateOnly(2026, 3, 2), Description = $"{marker} coffee" });

        var connection = new GoogleDriveConnection { UserId = userId, Name = $"{marker} folder", FolderId = "f", FolderUrl = "u" };
        var snapshot = new SpreadsheetBudgetSnapshot
            { UserId = userId, Connection = connection, FileName = $"{marker}.xlsx", FileId = "file", CategoryBreakdownJson = "{}" };
        db.SpreadsheetTransactionRecords.Add(new SpreadsheetTransactionRecord
            { UserId = userId, Snapshot = snapshot, Connection = connection, Description = $"{marker} rent" });

        db.SavingsGoals.Add(new SavingsGoal { UserId = userId, Name = $"{marker} goal" });
        db.SavedArticles.Add(new SavedArticle { UserId = userId, Title = $"{marker} article", Url = "https://example.com" });

        var story = new CommunityStory { UserId = userId, AuthorName = marker, Title = $"{marker} story" };
        var comment = new CommunityComment { UserId = userId, AuthorName = marker, Text = $"{marker} comment" };
        db.CommunityStoryLikes.Add(new CommunityStoryLike { UserId = userId, Story = story });
        db.CommunityCommentLikes.Add(new CommunityCommentLike { UserId = userId, Comment = comment });
        var poll = new CommunityPoll { PollCode = $"{marker}-poll", Question = $"{marker} question?" };
        var option = new CommunityPollOption { Poll = poll, Text = "Yes" };
        db.CommunityPollVotes.Add(new CommunityPollVote { UserId = userId, Poll = poll, Option = option });

        db.Feedback.Add(new FeedbackEntity { UserId = userId, Category = FeedbackCategory.Idea, Message = $"{marker} idea" });
        db.Notifications.Add(new Notification { UserId = userId, Title = $"{marker} notice", Message = "m" });
        db.AiUsageRecords.Add(new AiUsageRecord { UserId = userId, Endpoint = $"{marker}-chat" });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
