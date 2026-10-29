using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.AccountLifecycle;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.DataExport;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests.AccountLifecycle;

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
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DataExport_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static DataExportService CreateService(AppDbContext db) => new(db, new FixedTimeProvider(Now));

    [Fact]
    public async Task Export_ContainsTheAccountAndItsRecords()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId, UserName = "me@example.com", Email = "me@example.com" });
        db.Roles.Add(new IdentityRole { Id = "role-user", Name = "User" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = UserId, RoleId = "role-user" });
        db.UserLogins.Add(new IdentityUserLogin<string>
        { UserId = UserId, LoginProvider = "Google", ProviderKey = "g-1", ProviderDisplayName = "Google" });
        db.UserConsents.Add(new UserConsent
        {
            UserId = UserId,
            PolicyVersion = "1.0",
            AcceptedTerms = true,
            AcceptedPrivacy = true,
            IpAddress = "203.0.113.7",
            CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)
        });
        db.SavingsGoals.Add(new SavingsGoal { UserId = UserId, Name = "Emergency fund", TargetAmount = 10_000m });
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal(Now.UtcDateTime, export.ExportedAtUtc);
        Assert.Equal("me@example.com", export.Account.Email);
        Assert.Equal(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), export.Account.RegisteredAtUtc);
        Assert.Equal(["User"], export.Account.Roles);
        Assert.Equal(["Google"], export.Account.ExternalLogins);
        Assert.Equal("203.0.113.7", Assert.Single(export.Consents).IpAddress);
        Assert.Equal("Emergency fund", Assert.Single(export.SavingsGoals).Name);
    }

    [Fact]
    public async Task Export_JourneyProgress_CarriesCompletionsCardsAndThePreference_WithoutDeletedRows()
    {
        await using AppDbContext db = CreateContext();
        var first = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc);
        db.LessonCompletions.Add(new LessonCompletion
        { UserId = UserId, LessonSlug = "borrow-wisely", FinishedAt = first.AddDays(1), TimeZoneId = "Asia/Ho_Chi_Minh" });
        db.LessonCompletions.Add(new LessonCompletion
        { UserId = UserId, LessonSlug = "build-a-cushion", FinishedAt = first, TimeZoneId = "Asia/Ho_Chi_Minh" });
        db.LessonCompletions.Add(new LessonCompletion
        { UserId = UserId, LessonSlug = "own-a-piece", FinishedAt = first.AddDays(2), IsDeleted = true });
        db.ReviewCardStates.Add(new ReviewCardState
        { UserId = UserId, TermSlug = "emergency-fund", KnewIt = false, KnewItStreak = 0, LastShownAt = first.AddDays(1) });
        db.ReviewCardStates.Add(new ReviewCardState
        { UserId = UserId, TermSlug = "apr", KnewIt = true, KnewItStreak = 2, LastShownAt = first.AddDays(2), IsDeleted = true });
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "en", ShowJourneyOnDashboard = false });
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal(["build-a-cushion", "borrow-wisely"], export.LessonCompletions.Select(c => c.LessonSlug));
        Assert.Equal((first, "Asia/Ho_Chi_Minh"), (export.LessonCompletions[0].FinishedAtUtc, export.LessonCompletions[0].TimeZone));
        DataExportReviewCardDto card = Assert.Single(export.ReviewCards);
        Assert.Equal(("emergency-fund", false, 0), (card.TermSlug, card.KnewIt, card.KnewItStreak));
        Assert.False(export.ShowJourneyOnDashboard);
    }

    [Fact]
    public async Task Export_NeverContainsAnotherAccountsRecords()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "mine");
        UserDataSeed.OneOfEverything(db, OtherUserId, "theirs");
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        // Exactly one record per section, so a section without a text field cannot leak the other Account's row.
        AssertOneOfEverything(export);
        Assert.Equal("mine name", export.Community.Pseudonym);
        Assert.Equal((await db.CommunityStrategies.SingleAsync(s => s.UserId == UserId)).Id,
            Assert.Single(export.Community.Comments).StrategyId);
        string json = JsonSerializer.Serialize(export);
        Assert.Contains("mine", json);
        Assert.DoesNotContain("theirs", json);
    }

    [Fact]
    public async Task Export_PriorityWalks_CarryEveryAnswer_OldestFirst_WithoutDeletedOrOtherAccountsWalks()
    {
        await using AppDbContext db = CreateContext();
        var newer = new PriorityWalk
        {
            UserId = UserId,
            MonthlyAmount = 500.00m,
            Purpose = "A house for my family",
            DebtApr = 22.99m,
            DebtBalance = 5000.00m,
            EmergencyFundMonths = 1.5m,
            MonthlyExpenses = 3200.50m,
            HasFullEmployerMatch = false,
            EmployerMatchPercent = 4.50m,
            HorizonYears = 12,
            AgeBand = "From50To59Or64Plus",
            AtContributionLimit = true,
            TreeVersion = 1,
            LimitsTaxYear = 2026,
            Path = "S,Q1,L1",
            LeafId = "L1",
            PurposeNote = "Paying the card first frees money for the house.",
            CreatedAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var older = new PriorityWalk
        {
            UserId = UserId,
            MonthlyAmount = 250.00m,
            DebtApr = 0m,
            TreeVersion = 1,
            Path = "S,Q1,Q2,Q3,Q4,L5",
            LeafId = "L5",
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var deleted = new PriorityWalk
        { UserId = UserId, MonthlyAmount = 1m, TreeVersion = 1, Path = "S,Q1,L1", LeafId = "deleted" };
        deleted.SoftDelete();
        var theirs = new PriorityWalk
        { UserId = OtherUserId, MonthlyAmount = 2m, TreeVersion = 1, Path = "S,Q1,L1", LeafId = "theirs" };
        db.PriorityWalks.AddRange(newer, older, deleted, theirs);
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal(["L5", "L1"], export.PriorityWalks.Select(w => w.LeafId));
        DataExportPriorityWalkDto walk = export.PriorityWalks[1];
        Assert.Equal(500.00m, walk.MonthlyAmount);
        Assert.Equal("A house for my family", walk.Purpose);
        Assert.Equal(22.99m, walk.DebtApr);
        Assert.Equal(5000.00m, walk.DebtBalance);
        Assert.Equal(1.5m, walk.EmergencyFundMonths);
        Assert.Equal(3200.50m, walk.MonthlyExpenses);
        Assert.False(walk.HasFullEmployerMatch);
        Assert.Equal(4.50m, walk.EmployerMatchPercent);
        Assert.Equal(12, walk.HorizonYears);
        Assert.Equal("From50To59Or64Plus", walk.AgeBand);
        Assert.True(walk.AtContributionLimit);
        Assert.Equal(1, walk.TreeVersion);
        Assert.Equal(2026, walk.LimitsTaxYear);
        Assert.Equal("S,Q1,L1", walk.Path);
        Assert.Equal("Paying the card first frees money for the house.", walk.PurposeNote);
        Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), walk.CreatedAtUtc);

        // Questions not asked on the path stay null rather than turning into zeros or false.
        DataExportPriorityWalkDto unasked = export.PriorityWalks[0];
        Assert.Null(unasked.Purpose);
        Assert.Null(unasked.DebtBalance);
        Assert.Null(unasked.HasFullEmployerMatch);
        Assert.Null(unasked.AgeBand);
        Assert.Null(unasked.LimitsTaxYear);
    }

    [Fact]
    public void PriorityWalkSection_HoldsNoIdOrAccountId()
    {
        string[] properties = typeof(DataExportPriorityWalkDto).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("UserId", properties);
        Assert.DoesNotContain("IsDeleted", properties);
        Assert.DoesNotContain("DeletedAt", properties);
    }

    [Fact]
    public async Task Export_LeavesOutSoftDeletedAiUsage()
    {
        await using AppDbContext db = CreateContext();
        var deleted = new AiUsageRecord { UserId = UserId, Endpoint = "deleted-chat" };
        deleted.SoftDelete();
        db.AiUsageRecords.AddRange(deleted, new AiUsageRecord { UserId = UserId, Endpoint = "live-chat" });
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal("live-chat", Assert.Single(export.AiUsage).Endpoint);
    }

    private static void AssertOneOfEverything(DataExportDto export)
    {
        Assert.Single(export.Consents);
        Assert.Single(export.NetWorthParameters);
        Assert.Single(export.BudgetAllocations);
        Assert.Equal("BudgetTracker", export.BudgetDataSource);
        Assert.Equal("vi", export.Language);
        Assert.True(export.ShowJourneyOnDashboard);
        Assert.Single(export.LessonCompletions);
        Assert.Single(export.ReviewCards);
        Assert.Equal("Expense", Assert.Single(export.BudgetCategories).Kind);
        Assert.Single(export.BudgetMonthLines);
        Assert.Single(export.BudgetTransactions);
        Assert.Single(export.GoogleDriveConnections);
        Assert.Single(export.SpreadsheetMonths);
        Assert.Single(export.SpreadsheetTransactions);
        Assert.Equal("Regular", Assert.Single(Assert.Single(export.SavingsGoals).Contributions).Kind);
        Assert.Single(export.SavedArticles);
        Assert.Single(export.PriorityWalks);
        Assert.Equal("Approved", Assert.Single(export.Community.Strategies).Status);
        Assert.Single(export.Community.Comments);
        Assert.Single(export.Community.CommentLikes);
        Assert.Equal("Yes", Assert.Single(export.Community.PollVotes).Answer);
        Assert.Equal("Idea", Assert.Single(export.Feedback).Category);
        Assert.Single(export.Notifications);
        Assert.Single(export.AiUsage);
    }

    [Fact]
    public async Task Export_LeavesOutSoftDeletedRows()
    {
        await using AppDbContext db = CreateContext();
        var live = new SavingsGoal { UserId = UserId, Name = "Live goal" };
        var deleted = new SavingsGoal { UserId = UserId, Name = "Deleted goal" };
        deleted.SoftDelete();
        db.SavingsGoals.AddRange(live, deleted);
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal("Live goal", Assert.Single(export.SavingsGoals).Name);
    }

    [Fact]
    public async Task Export_LeavesOutSoftDeletedTransactionsAndBudgetLines()
    {
        await using AppDbContext db = CreateContext();
        var category = new BudgetCategory { UserId = UserId, Name = "Food", Kind = BudgetCategoryKind.Expense };
        var plan = new MonthlyBudgetPlan { UserId = UserId, Year = 2026, Month = 3 };
        var deletedTransaction = new BudgetTransaction
        { UserId = UserId, Category = category, Date = new DateOnly(2026, 3, 1), Description = "Deleted" };
        var deletedLine = new MonthlyBudgetLine { UserId = UserId, Plan = plan, Category = category, PlannedAmount = 9m };
        deletedTransaction.SoftDelete();
        deletedLine.SoftDelete();
        db.BudgetTransactions.AddRange(deletedTransaction, new BudgetTransaction
        { UserId = UserId, Category = category, Date = new DateOnly(2026, 3, 2), Description = "Live" });
        db.MonthlyBudgetLines.Add(deletedLine);
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Equal("Live", Assert.Single(export.BudgetTransactions).Description);
        Assert.Empty(export.BudgetMonthLines);
    }

    [Fact]
    public async Task Export_TransactionInADeletedCategory_KeepsTheCategoryName()
    {
        await using AppDbContext db = CreateContext();
        var category = new BudgetCategory { UserId = UserId, Name = "Old category", Kind = BudgetCategoryKind.Expense };
        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync();
        db.BudgetTransactions.Add(new BudgetTransaction
        { UserId = UserId, CategoryId = category.Id, Date = new DateOnly(2026, 3, 1), Amount = 12.34m });
        category.SoftDelete();
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        Assert.Empty(export.BudgetCategories);
        DataExportBudgetTransactionDto transaction = Assert.Single(export.BudgetTransactions);
        Assert.Equal("Old category", transaction.Category);
        Assert.Equal(12.34m, transaction.Amount);
    }

    [Fact]
    public async Task Export_NeverContainsGoogleDriveApiKeys()
    {
        await using AppDbContext db = CreateContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            UserId = UserId,
            Name = "Budget folder",
            FolderId = "folder",
            FolderUrl = "https://drive.example/folder",
            EncryptedApiKey = "encrypted-secret-key",
            MaskedApiKey = "AIza****cret"
        });
        await db.SaveChangesAsync();

        DataExportDto export = await CreateService(db).ExportAsync(UserId);

        string json = JsonSerializer.Serialize(export);
        Assert.Contains("Budget folder", json);
        Assert.DoesNotContain("encrypted-secret-key", json);
        Assert.DoesNotContain("AIza", json);
    }

    [Fact]
    public async Task Export_UnknownAccount_ReturnsEmptySections()
    {
        await using AppDbContext db = CreateContext();

        DataExportDto export = await CreateService(db).ExportAsync("nobody");

        Assert.Equal("nobody", export.Account.Id);
        Assert.Null(export.Account.Email);
        Assert.Null(export.Account.RegisteredAtUtc);
        Assert.Null(export.BudgetDataSource);
        Assert.Null(export.Language);
        Assert.Empty(export.BudgetTransactions);
        Assert.Empty(export.Community.Strategies);
    }

    [Fact]
    public void JoinQueries_TranslateToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        DataExportService service = CreateService(db);

        AzureSqlTranslation.AssertTranslates(service.BudgetTransactionsQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.BudgetMonthLinesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.PollVotesQuery(UserId));
        AzureSqlTranslation.AssertTranslates(service.AccountRolesQuery(UserId));
    }
}
