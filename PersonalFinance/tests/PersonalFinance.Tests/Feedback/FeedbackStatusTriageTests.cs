using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Feedback Status triage on <see cref="IFeedbackService"/>: an Admin moves Feedback between New, Reviewed and
/// Archived, and filters the inbox by status with Archived hidden by default.
/// </summary>
public class FeedbackStatusTriageTests
{
    private static readonly DateTime Monday = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext(string? name = null)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"FeedbackTriage_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static readonly DateTime Tuesday = new(2026, 10, 20, 14, 30, 0, DateTimeKind.Utc);

    private static FeedbackService CreateService(AppDbContext db) =>
        new(db, new RateLimiterService(5, TimeSpan.FromHours(1)), new FixedTimeProvider(Tuesday));

    private static FeedbackEntity Feedback(string message, FeedbackStatus status = FeedbackStatus.New) => new()
    {
        UserId = "user-1",
        Category = FeedbackCategory.Idea,
        Message = message,
        Status = status,
        CreatedAt = Monday
    };

    [Theory]
    [InlineData(FeedbackStatus.New, FeedbackStatus.Reviewed)]
    [InlineData(FeedbackStatus.Reviewed, FeedbackStatus.Archived)]
    [InlineData(FeedbackStatus.Archived, FeedbackStatus.New)]
    [InlineData(FeedbackStatus.Reviewed, FeedbackStatus.New)]
    public async Task ChangeStatus_Persists(FeedbackStatus from, FeedbackStatus to)
    {
        string dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int id;
        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity feedback = Feedback("triage me", from);
            db.Feedback.Add(feedback);
            await db.SaveChangesAsync();
            id = feedback.Id;
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            Assert.True(await CreateService(db).ChangeStatusAsync(id, to));
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackAdminItemDto item = Assert.Single(await CreateService(db).ListAsync(to));
            Assert.Equal(id, item.Id);
            Assert.Equal(to, item.Status);
        }
    }

    [Fact]
    public async Task ChangeStatus_SetsUpdatedAtToTheCurrentUtcTime()
    {
        string dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int id;
        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity feedback = Feedback("triage me");
            db.Feedback.Add(feedback);
            await db.SaveChangesAsync();
            id = feedback.Id;
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            Assert.True(await CreateService(db).ChangeStatusAsync(id, FeedbackStatus.Reviewed));
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity stored = await db.Feedback.SingleAsync(f => f.Id == id);
            Assert.Equal(Tuesday, stored.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt!.Value.Kind);
        }
    }

    [Fact]
    public async Task List_WithoutAStatus_HidesArchivedFeedback()
    {
        await using AppDbContext db = CreateContext();
        db.Feedback.AddRange(
            Feedback("new", FeedbackStatus.New),
            Feedback("reviewed", FeedbackStatus.Reviewed),
            Feedback("archived", FeedbackStatus.Archived));
        await db.SaveChangesAsync();

        List<FeedbackAdminItemDto> items = await CreateService(db).ListAsync();

        Assert.Equal(["new", "reviewed"], items.Select(i => i.Message).Order());
    }

    [Theory]
    [InlineData(FeedbackStatus.New, "new")]
    [InlineData(FeedbackStatus.Reviewed, "reviewed")]
    [InlineData(FeedbackStatus.Archived, "archived")]
    public async Task List_WithAStatus_ShowsOnlyFeedbackWithThatStatus(FeedbackStatus status, string expected)
    {
        await using AppDbContext db = CreateContext();
        db.Feedback.AddRange(
            Feedback("new", FeedbackStatus.New),
            Feedback("reviewed", FeedbackStatus.Reviewed),
            Feedback("archived", FeedbackStatus.Archived));
        await db.SaveChangesAsync();

        List<FeedbackAdminItemDto> items = await CreateService(db).ListAsync(status);

        Assert.Equal(expected, Assert.Single(items).Message);
    }

    [Fact]
    public async Task ChangeStatus_UnknownFeedback_IsNotFound_AndLeavesOtherFeedbackUnchanged()
    {
        string dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int otherId;
        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity other = Feedback("other");
            db.Feedback.Add(other);
            await db.SaveChangesAsync();
            otherId = other.Id;
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            Assert.False(await CreateService(db).ChangeStatusAsync(otherId + 1, FeedbackStatus.Reviewed));
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity stored = await db.Feedback.SingleAsync(f => f.Id == otherId);
            Assert.Equal(FeedbackStatus.New, stored.Status);
            Assert.Null(stored.UpdatedAt);
        }
    }

    [Fact]
    public async Task ChangeStatus_SoftDeletedFeedback_IsNotFound_AndStaysUnchanged()
    {
        string dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int id;
        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity deleted = Feedback("deleted");
            deleted.SoftDelete();
            db.Feedback.Add(deleted);
            await db.SaveChangesAsync();
            id = deleted.Id;
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            Assert.False(await CreateService(db).ChangeStatusAsync(id, FeedbackStatus.Archived));
        }

        await using (AppDbContext db = CreateContext(dbName))
        {
            FeedbackEntity stored = await db.Feedback.IgnoreQueryFilters().SingleAsync(f => f.Id == id);
            Assert.Equal(FeedbackStatus.New, stored.Status);
            Assert.Null(stored.UpdatedAt);
        }
    }

    [Fact]
    public void AdminListQuery_DefaultInbox_TranslatesToAzureSql_ExcludingArchived()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(CreateService(db).AdminListQuery());

        Assert.Contains("[f].[Status] <> 3", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminListQuery_WithAStatus_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(CreateService(db).AdminListQuery(FeedbackStatus.Reviewed));

        Assert.Contains("[f].[Status] =", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY [f].[CreatedAt] DESC, [f].[Id] DESC", sql, StringComparison.Ordinal);
    }
}
