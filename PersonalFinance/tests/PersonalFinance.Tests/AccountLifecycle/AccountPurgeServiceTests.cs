using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.AccountLifecycle;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.AccountLifecycle;

/// <summary>
/// Behaviour of <see cref="IAccountPurgeService"/> against an in-memory database: when the Grace Period ends, every
/// trace of the Account goes in one save, except its Guild Hall stories and comments, which stay under the placeholder
/// id without its Pseudonym.
/// </summary>
public class AccountPurgeServiceTests
{
    private const string UserId = "leaving-user";
    private const string OtherUserId = "staying-user";
    private static readonly DateTimeOffset RequestedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset GraceEnds = RequestedAt.AddDays(30);

    private readonly string _databaseName = $"AccountPurge_{Guid.NewGuid():N}";

    private AppDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(interceptors)
            .Options);

    private static AccountPurgeService CreatePurge(AppDbContext db, DateTimeOffset now) =>
        new(db, new FixedTimeProvider(now), NullLogger<AccountPurgeService>.Instance);

    /// <summary>Seeds both Accounts and makes a Deletion Request for <see cref="UserId"/>; returns its cancel token.</summary>
    private async Task<string> SeedPendingRequestAsync()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "leaving");
        UserDataSeed.OneOfEverything(db, OtherUserId, "staying");
        db.Roles.Add(new IdentityRole { Id = "role-user", Name = AppRoles.User });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = UserId, RoleId = "role-user" });
        db.UserClaims.Add(new IdentityUserClaim<string>
        { UserId = UserId, ClaimType = "urn:google:picture", ClaimValue = "p" });
        db.UserLogins.Add(new IdentityUserLogin<string>
        { UserId = UserId, LoginProvider = "Google", ProviderKey = "g-1", ProviderDisplayName = "Google" });
        db.UserTokens.Add(new IdentityUserToken<string>
        { UserId = UserId, LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = "k" });
        var deletedEarlier = new SavingsGoal { UserId = UserId, Name = "Deleted in March" };
        deletedEarlier.SoftDelete();
        db.SavingsGoals.Add(deletedEarlier);
        await db.SaveChangesAsync();

        foreach ((string? owner, string? file) in new[] { (UserId, "leaving.xlsx"), (OtherUserId, "staying.xlsx") })
        {
            int connectionId = await db.GoogleDriveConnections
                .Where(c => c.UserId == owner).Select(c => c.Id).SingleAsync();
            db.GoogleDriveCachedFiles.Add(new GoogleDriveCachedFile { ConnectionId = connectionId, DriveFileId = file, Name = file });
        }

        await db.SaveChangesAsync();

        DeletionRequestResult result = await new AccountDeletionService(db, new FixedTimeProvider(RequestedAt)).RequestAsync(UserId);
        Assert.Equal(DeletionRequestOutcome.Accepted, result.Outcome);
        return result.CancelToken!;
    }

    [Fact]
    public async Task Purge_ErasesEveryTraceOfTheAccount_AndLeavesOtherAccountsAlone()
    {
        await SeedPendingRequestAsync();
        await using AppDbContext db = CreateContext();
        int otherRowsBefore = await CountRowsAsync(db, OtherUserId);

        AccountPurgeSummary summary = await CreatePurge(db, GraceEnds).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(1, 0, 0), summary);
        await using AppDbContext check = CreateContext();
        foreach (Type type in UserOwnedTypes(check))
        {
            Assert.Empty(await RowsAsync(check, type, UserId));
        }

        Assert.False(await check.InvestmentScenarios.IgnoreQueryFilters().AnyAsync(s => s.UserId == UserId));
        Assert.True(await check.InvestmentScenarios.AnyAsync(s => s.UserId == OtherUserId));
        Assert.False(await check.Users.AnyAsync(u => u.Id == UserId));
        Assert.False(await check.UserRoles.AnyAsync(r => r.UserId == UserId));
        Assert.False(await check.UserClaims.AnyAsync(c => c.UserId == UserId));
        Assert.False(await check.UserLogins.AnyAsync(l => l.UserId == UserId));
        Assert.False(await check.UserTokens.AnyAsync(t => t.UserId == UserId));
        Assert.False(await check.DeletionRequests.AnyAsync(r => r.UserId == UserId));
        Assert.Equal("staying.xlsx", Assert.Single(check.GoogleDriveCachedFiles).Name);
        Assert.Equal(otherRowsBefore, await CountRowsAsync(check, OtherUserId));
    }

    [Fact]
    public async Task Purge_KeepsTheAccountsApprovedStrategiesAndComments_WithoutItsPseudonym_AndErasesItsUnapprovedOnes()
    {
        await SeedPendingRequestAsync();
        await using (AppDbContext seed = CreateContext())
        {
            seed.CommunityStrategies.AddRange(
                new CommunityStrategy { UserId = UserId, Title = "leaving pending strategy" },
                new CommunityStrategy { UserId = UserId, Title = "leaving rejected strategy", Status = CommunityStrategyStatus.Rejected },
                new CommunityStrategy { UserId = OtherUserId, Title = "staying pending strategy" });
            await seed.SaveChangesAsync();
        }

        await using AppDbContext db = CreateContext();

        await CreatePurge(db, GraceEnds).PurgeDueAsync();

        await using AppDbContext check = CreateContext();
        CommunityStrategy strategy = await check.CommunityStrategies.SingleAsync(s => s.Title == "leaving strategy");
        Assert.Equal(DeletedAccount.UserId, strategy.UserId);
        Assert.False(await check.CommunityStrategies.IgnoreQueryFilters()
            .AnyAsync(s => s.Title == "leaving pending strategy" || s.Title == "leaving rejected strategy"));
        Assert.True(await check.CommunityStrategies.AnyAsync(s => s.Title == "staying pending strategy"));
        CommunityComment comment = await check.CommunityComments.SingleAsync(c => c.Text == "leaving comment");
        Assert.Equal(DeletedAccount.UserId, comment.UserId);
        Assert.Equal("staying name", Assert.Single(check.CommunityPseudonyms.IgnoreQueryFilters()).Name);
    }

    [Fact]
    public async Task Purge_KeepsOnlyARecordOfWhen_UnderThePlaceholderId()
    {
        await SeedPendingRequestAsync();
        await using AppDbContext db = CreateContext();

        await CreatePurge(db, GraceEnds).PurgeDueAsync();

        await using AppDbContext check = CreateContext();
        DeletionRequest request = await check.DeletionRequests.SingleAsync();
        Assert.Equal(DeletionRequestStatus.Purged, request.Status);
        Assert.Equal(DeletedAccount.UserId, request.UserId);
        Assert.Equal(RequestedAt.UtcDateTime, request.RequestedAt);
        Assert.Equal(GraceEnds.UtcDateTime, request.PurgedAt);
    }

    [Fact]
    public async Task Purge_BeforeTheGracePeriodEnds_ChangesNothing()
    {
        await SeedPendingRequestAsync();
        await using AppDbContext db = CreateContext();

        AccountPurgeSummary summary = await CreatePurge(db, GraceEnds.AddSeconds(-1)).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(0, 0, 0), summary);
        await using AppDbContext check = CreateContext();
        Assert.True(await check.Users.AnyAsync(u => u.Id == UserId));
        Assert.Equal(DeletionRequestStatus.Pending, (await check.DeletionRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Purge_OfACancelledRequest_ChangesNothing()
    {
        string token = await SeedPendingRequestAsync();
        await using (AppDbContext cancel = CreateContext())
        {
            Assert.True(await new AccountDeletionService(cancel, new FixedTimeProvider(RequestedAt.AddDays(1)))
                .CancelAsync(token));
        }

        await using AppDbContext db = CreateContext();
        AccountPurgeSummary summary = await CreatePurge(db, GraceEnds.AddDays(5)).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(0, 0, 0), summary);
        await using AppDbContext check = CreateContext();
        Assert.True(await check.Users.AnyAsync(u => u.Id == UserId));
        Assert.True(await check.SavingsGoals.AnyAsync(g => g.UserId == UserId));
    }

    [Fact]
    public async Task Purge_RunTwice_IsSafe()
    {
        await SeedPendingRequestAsync();
        await using (AppDbContext first = CreateContext())
        {
            await CreatePurge(first, GraceEnds).PurgeDueAsync();
        }

        await using AppDbContext second = CreateContext();
        AccountPurgeSummary summary = await CreatePurge(second, GraceEnds.AddDays(1)).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(0, 0, 0), summary);
    }

    [Fact]
    public async Task Purge_CancelledWhileRunning_IsSkipped_AndErasesNothing()
    {
        string token = await SeedPendingRequestAsync();
        var cancelBeforeSave = new BeforeSaveInterceptor(async () =>
        {
            await using AppDbContext cancel = CreateContext();
            Assert.True(await new AccountDeletionService(cancel, new FixedTimeProvider(GraceEnds.AddSeconds(-1)))
                .CancelAsync(token));
        });
        await using AppDbContext db = CreateContext(cancelBeforeSave);

        AccountPurgeSummary summary = await CreatePurge(db, GraceEnds).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(0, 1, 0), summary);
        await using AppDbContext check = CreateContext();
        Assert.True(await check.Users.AnyAsync(u => u.Id == UserId));
        Assert.True(await check.SavingsGoals.AnyAsync(g => g.UserId == UserId && g.Name == "leaving goal"));
        Assert.Equal(DeletionRequestStatus.Cancelled, (await check.DeletionRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Purge_HardDeletesStrategiesAndCommentsTheAccountDeletedItself_WithOtherPeoplesReactions()
    {
        await using (AppDbContext seed = CreateContext())
        {
            seed.Users.Add(new IdentityUser { Id = UserId });
            var deletedStrategy = new CommunityStrategy
            { UserId = UserId, Title = "deleted strategy", Status = CommunityStrategyStatus.Approved };
            var deletedComment = new CommunityComment { UserId = UserId, Text = "deleted comment" };
            deletedStrategy.SoftDelete();
            deletedComment.SoftDelete();
            seed.CommunityStrategies.Add(deletedStrategy);
            var pendingStrategy = new CommunityStrategy
            { UserId = UserId, Title = "pending strategy", Status = CommunityStrategyStatus.Pending };
            seed.CommunityReactions.Add(new CommunityReaction
            { UserId = OtherUserId, Comment = deletedComment, Kind = CommunityReactionKind.CheeringYouOn });
            seed.CommunityReactions.Add(new CommunityReaction
            { UserId = OtherUserId, Strategy = deletedStrategy, Kind = CommunityReactionKind.Helpful });
            seed.CommunityReactions.Add(new CommunityReaction
            { UserId = OtherUserId, Strategy = pendingStrategy, Kind = CommunityReactionKind.Helpful });
            await seed.SaveChangesAsync();
            await new AccountDeletionService(seed, new FixedTimeProvider(RequestedAt)).RequestAsync(UserId);
        }

        await using AppDbContext db = CreateContext();
        await CreatePurge(db, GraceEnds).PurgeDueAsync();

        await using AppDbContext check = CreateContext();
        Assert.Empty(check.CommunityStrategies.IgnoreQueryFilters());
        Assert.Empty(check.CommunityComments.IgnoreQueryFilters());
        Assert.Empty(check.CommunityReactions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Purge_TwoRunsOverlapping_EraseTheAccountOnce_AndTheLateRunSkipsIt()
    {
        await SeedPendingRequestAsync();
        var otherRunFirst = new BeforeSaveInterceptor(async () =>
        {
            await using AppDbContext other = CreateContext();
            Assert.Equal(new AccountPurgeSummary(1, 0, 0), await CreatePurge(other, GraceEnds).PurgeDueAsync());
        });
        await using AppDbContext db = CreateContext(otherRunFirst);

        AccountPurgeSummary summary = await CreatePurge(db, GraceEnds).PurgeDueAsync();

        Assert.Equal(new AccountPurgeSummary(0, 1, 0), summary);
        await using AppDbContext check = CreateContext();
        Assert.Equal(DeletionRequestStatus.Purged, (await check.DeletionRequests.SingleAsync()).Status);
        Assert.False(await check.Users.AnyAsync(u => u.Id == UserId));
    }

    [Fact]
    public void EveryTableWithAUserId_IsUserOwned_OrHandledByThePurge()
    {
        using AppDbContext db = CreateContext();
        // Not AuditableEntity, removed or anonymized by name in AccountPurgeService. A new table holding an Account id
        // must either inherit AuditableEntity (covered automatically) or be handled there and added here.
        Type[] handledExplicitly =
        [
            typeof(IdentityUserRole<string>), typeof(IdentityUserClaim<string>), typeof(IdentityUserLogin<string>),
            typeof(IdentityUserToken<string>), typeof(DeletionRequest)
        ];

        var unhandled = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("UserId") is not null)
            .Select(t => t.ClrType)
            .Where(t => !typeof(AuditableEntity).IsAssignableFrom(t) && !handledExplicitly.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unhandled);
    }

    [Fact]
    public void DeletedEntityTypes_CoverEveryUserOwnedEntity_ExceptStrategiesAndComments()
    {
        using AppDbContext db = CreateContext();

        IReadOnlyList<Type> deleted = CreatePurge(db, GraceEnds).DeletedEntityTypes();

        IOrderedEnumerable<Type> expected = UserOwnedTypes(db)
            .Where(t => t != typeof(CommunityStrategy) && t != typeof(CommunityComment))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
        Assert.Equal(expected, deleted);
        Assert.Contains(typeof(AiUsageRecord), deleted);
        Assert.Contains(typeof(InvestmentScenario), deleted);
    }

    [Fact]
    public void DueRequestsQuery_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        AzureSqlTranslation.AssertTranslates(CreatePurge(db, GraceEnds).DueRequestsQuery(GraceEnds.UtcDateTime));
    }

    [Theory]
    [InlineData(new[] { "--purge-only" }, true)]
    [InlineData(new[] { "--migrate-only" }, false)]
    [InlineData(new string[0], false)]
    public void PurgeOnly_IsRequestedByTheArgument(string[] args, bool expected)
    {
        Assert.Equal(expected, AccountPurgeJob.IsRequested(args));
    }

    private static List<Type> UserOwnedTypes(AppDbContext db) =>
        db.Model.GetEntityTypes().Select(t => t.ClrType)
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t)).ToList();

    private static async Task<int> CountRowsAsync(AppDbContext db, string userId)
    {
        int total = 0;
        foreach (Type type in UserOwnedTypes(db))
        {
            total += (await RowsAsync(db, type, userId)).Count;
        }

        return total;
    }

    private static async Task<List<AuditableEntity>> RowsAsync(AppDbContext db, Type entityType, string userId)
    {
        object set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType).Invoke(db, null)!;
        MethodInfo ignore = typeof(EntityFrameworkQueryableExtensions).GetMethods()
            .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters)
                         && m.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);
        List<AuditableEntity> rows = await ((IQueryable)ignore.Invoke(null, [set])!).Cast<AuditableEntity>().ToListAsync();
        return rows.Where(row => row.UserId == userId).ToList();
    }

    /// <summary>Runs an action once, just before the first save of the context it is attached to.</summary>
    private sealed class BeforeSaveInterceptor(Func<Task> action) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await action();
            }

            return result;
        }
    }
}
