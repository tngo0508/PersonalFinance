using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.AccountLifecycle;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.AccountDeletion;

/// <summary>
/// Behaviour of <see cref="IAccountDeletionService"/> against an in-memory database: a Deletion Request hides every
/// user-owned row with one stamp and locks the Account; the single-use cancel token restores exactly those rows.
/// </summary>
public class AccountDeletionServiceTests
{
    private const string UserId = "leaving-user";
    private const string OtherUserId = "staying-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, 123, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AccountDeletion_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AccountDeletionService CreateService(AppDbContext db, DateTimeOffset? now = null) =>
        new(db, new FixedTimeProvider(now ?? Now));

    [Fact]
    public async Task Request_HidesEveryUserOwnedRow_WithOneStamp_AndLeavesOtherAccountsAlone()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "leaving");
        UserDataSeed.OneOfEverything(db, OtherUserId, "staying");
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);

        DeletionRequestResult result = await service.RequestAsync(UserId);

        Assert.Equal(DeletionRequestOutcome.Accepted, result.Outcome);
        foreach (Type type in service.HiddenEntityTypes())
        {
            List<AuditableEntity> leaving = await RowsAsync(db, type, UserId);
            Assert.True(leaving.Count > 0, $"UserDataSeed.OneOfEverything seeds no {type.Name}: add one.");
            Assert.All(leaving, row =>
            {
                Assert.True(row.IsDeleted, $"{type.Name} was not hidden.");
                Assert.Equal(new DateTime(2026, 10, 22, 12, 0, 0, 123, DateTimeKind.Utc), row.DeletedAt);
            });
            Assert.All(await RowsAsync(db, type, OtherUserId), row => Assert.False(row.IsDeleted));
        }
    }

    [Fact]
    public void HiddenEntityTypes_CoverEveryUserOwnedEntity_ExceptTheAiSpendLedger()
    {
        using AppDbContext db = CreateContext();

        IReadOnlyList<Type> hidden = CreateService(db).HiddenEntityTypes();

        var userOwned = db.Model.GetEntityTypes().Select(t => t.ClrType)
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t)).ToList();
        Assert.Equal(userOwned.Count - 1, hidden.Count);
        Assert.DoesNotContain(typeof(AiUsageRecord), hidden);
        Assert.DoesNotContain(typeof(DeletionRequest), hidden);
    }

    [Fact]
    public async Task Request_LocksTheAccountOut_EndsItsSessions_AndStoresOnlyTheTokenHash()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId, LockoutEnabled = false, SecurityStamp = "old-stamp" });
        await db.SaveChangesAsync();

        DeletionRequestResult result = await CreateService(db).RequestAsync(UserId);

        IdentityUser user = await db.Users.SingleAsync(u => u.Id == UserId);
        Assert.True(user.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, user.LockoutEnd);
        Assert.NotEqual("old-stamp", user.SecurityStamp);

        DeletionRequest request = await db.DeletionRequests.SingleAsync();
        Assert.Equal(DeletionRequestStatus.Pending, request.Status);
        Assert.Equal(Now.UtcDateTime.AddDays(30), request.PurgeAfter);
        Assert.Equal(request.PurgeAfter, result.PurgeAfter);
        Assert.False(string.IsNullOrEmpty(result.CancelToken));
        Assert.DoesNotContain(result.CancelToken!, request.CancelTokenHash);
        Assert.Equal(64, request.CancelTokenHash.Length);
    }

    [Fact]
    public async Task Request_TheLastAdmin_IsRefused_AndChangesNothing()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "admin");
        AddAdmin(db, UserId);
        await db.SaveChangesAsync();

        DeletionRequestResult result = await CreateService(db).RequestAsync(UserId);

        Assert.Equal(DeletionRequestOutcome.LastAdmin, result.Outcome);
        Assert.Null(result.CancelToken);
        Assert.Empty(db.DeletionRequests);
        Assert.Single(db.SavingsGoals);
        Assert.Null((await db.Users.SingleAsync(u => u.Id == UserId)).LockoutEnd);
    }

    [Fact]
    public async Task Request_AnAdmin_WhenAnotherAdminStays_IsAccepted()
    {
        await using AppDbContext db = CreateContext();
        db.Users.AddRange(new IdentityUser { Id = UserId }, new IdentityUser { Id = OtherUserId });
        AddAdmin(db, UserId, OtherUserId);
        await db.SaveChangesAsync();

        DeletionRequestResult result = await CreateService(db).RequestAsync(UserId);

        Assert.Equal(DeletionRequestOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public async Task Request_AnAdmin_WhenTheOnlyOtherAdminIsLeavingToo_IsRefused()
    {
        await using AppDbContext db = CreateContext();
        db.Users.AddRange(new IdentityUser { Id = UserId }, new IdentityUser { Id = OtherUserId });
        AddAdmin(db, UserId, OtherUserId);
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);
        Assert.Equal(DeletionRequestOutcome.Accepted, (await service.RequestAsync(OtherUserId)).Outcome);

        DeletionRequestResult result = await service.RequestAsync(UserId);

        Assert.Equal(DeletionRequestOutcome.LastAdmin, result.Outcome);
    }

    [Fact]
    public async Task Request_Twice_IsRefusedTheSecondTime()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId });
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);
        await service.RequestAsync(UserId);

        DeletionRequestResult second = await service.RequestAsync(UserId);

        Assert.Equal(DeletionRequestOutcome.AlreadyPending, second.Outcome);
        Assert.Single(db.DeletionRequests);
    }

    [Fact]
    public async Task Request_UnknownAccount_IsNotFound()
    {
        await using AppDbContext db = CreateContext();

        DeletionRequestResult result = await CreateService(db).RequestAsync("nobody");

        Assert.Equal(DeletionRequestOutcome.AccountNotFound, result.Outcome);
        Assert.Empty(db.DeletionRequests);
    }

    [Fact]
    public async Task Cancel_RestoresExactlyTheRowsTheRequestHid_AndEndsTheLockout()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "leaving");
        var deletedEarlier = new SavingsGoal { UserId = UserId, Name = "Deleted in March" };
        deletedEarlier.SoftDelete();
        db.SavingsGoals.Add(deletedEarlier);
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);
        string token = (await service.RequestAsync(UserId)).CancelToken!;

        bool cancelled = await CreateService(db, Now.AddDays(29)).CancelAsync(token);

        Assert.True(cancelled);
        foreach (Type type in service.HiddenEntityTypes())
        {
            Assert.All(await RowsAsync(db, type, UserId), row =>
                Assert.Equal(ReferenceEquals(row, deletedEarlier), row.IsDeleted));
        }

        Assert.Null((await db.Users.SingleAsync(u => u.Id == UserId)).LockoutEnd);
        DeletionRequest request = await db.DeletionRequests.SingleAsync();
        Assert.Equal(DeletionRequestStatus.Cancelled, request.Status);
        Assert.Equal(Now.UtcDateTime.AddDays(29), request.CancelledAt);
    }

    [Fact]
    public async Task Cancel_WorksOnlyOnce()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId });
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);
        string token = (await service.RequestAsync(UserId)).CancelToken!;
        Assert.True(await service.CancelAsync(token));

        Assert.False(await service.CancelAsync(token));
    }

    [Fact]
    public async Task Cancel_AfterTheGracePeriod_DoesNothing()
    {
        await using AppDbContext db = CreateContext();
        UserDataSeed.OneOfEverything(db, UserId, "leaving");
        await db.SaveChangesAsync();
        string token = (await CreateService(db).RequestAsync(UserId)).CancelToken!;

        bool cancelled = await CreateService(db, Now.AddDays(30)).CancelAsync(token);

        Assert.False(cancelled);
        Assert.Empty(db.SavingsGoals);
        Assert.Equal(DateTimeOffset.MaxValue, (await db.Users.SingleAsync(u => u.Id == UserId)).LockoutEnd);
    }

    [Fact]
    public async Task Cancel_WithAnUnknownToken_DoesNothing()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId });
        await db.SaveChangesAsync();
        await CreateService(db).RequestAsync(UserId);

        Assert.False(await CreateService(db).CancelAsync("not-the-token"));
        Assert.Equal(DeletionRequestStatus.Pending, (await db.DeletionRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Request_And_Cancel_RotateTheConcurrencyStamp()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId, ConcurrencyStamp = "original" });
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);

        string token = (await service.RequestAsync(UserId)).CancelToken!;
        string? afterRequest = (await db.Users.SingleAsync()).ConcurrencyStamp;
        await service.CancelAsync(token);

        Assert.NotEqual("original", afterRequest);
        Assert.NotEqual(afterRequest, (await db.Users.SingleAsync()).ConcurrencyStamp);
    }

    [Fact]
    public async Task Cancel_OfAPurgedRequest_DoesNothing()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = UserId });
        await db.SaveChangesAsync();
        AccountDeletionService service = CreateService(db);
        string token = (await service.RequestAsync(UserId)).CancelToken!;
        (await db.DeletionRequests.SingleAsync()).Status = DeletionRequestStatus.Purged;
        await db.SaveChangesAsync();

        Assert.False(await service.CancelAsync(token));
        Assert.Equal(DeletionRequestStatus.Purged, (await db.DeletionRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeletionRequestStatus_IsAConcurrencyToken_SoTwoWritersCannotBothChangeIt()
    {
        string name = $"AccountDeletion_{Guid.NewGuid():N}";
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;
        await using (var seed = new AppDbContext(options))
        {
            seed.DeletionRequests.Add(new DeletionRequest { UserId = UserId, CancelTokenHash = "hash" });
            await seed.SaveChangesAsync();
        }

        await using var first = new AppDbContext(options);
        await using var second = new AppDbContext(options);
        DeletionRequest cancel = await first.DeletionRequests.SingleAsync();
        DeletionRequest purge = await second.DeletionRequests.SingleAsync();
        cancel.Status = DeletionRequestStatus.Cancelled;
        await first.SaveChangesAsync();

        purge.Status = DeletionRequestStatus.Purged;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public void OtherStayingAdminsQuery_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        AzureSqlTranslation.AssertTranslates(CreateService(db).OtherStayingAdminsQuery(UserId));
    }

    private static void AddAdmin(AppDbContext db, params string[] userIds)
    {
        var role = new IdentityRole { Id = "role-admin", Name = AppRoles.Admin };
        db.Roles.Add(role);
        foreach (string userId in userIds)
        {
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });
        }
    }

    private static async Task<List<AuditableEntity>> RowsAsync(AppDbContext db, Type entityType, string userId)
    {
        List<AuditableEntity> rows = await QueryAll(db, entityType).ToListAsync();
        return rows.Where(row => row.UserId == userId).ToList();
    }

    private static IQueryable<AuditableEntity> QueryAll(AppDbContext db, Type entityType)
    {
        object set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType).Invoke(db, null)!;
        MethodInfo ignore = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods().Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters)
                                      && m.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);
        return ((IQueryable)ignore.Invoke(null, [set])!).Cast<AuditableEntity>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
