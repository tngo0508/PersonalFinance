using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Shared.Enums;
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
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"FeedbackTriage_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static FeedbackService CreateService(AppDbContext db) =>
        new(db, new SubmitFeedbackRequestValidator(), FeedbackService.CreateRateLimiter());

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
        var dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int id;
        await using (var db = CreateContext(dbName))
        {
            var feedback = Feedback("triage me", from);
            db.Feedback.Add(feedback);
            await db.SaveChangesAsync();
            id = feedback.Id;
        }

        await using (var db = CreateContext(dbName))
        {
            Assert.True(await CreateService(db).ChangeStatusAsync(id, to));
        }

        await using (var db = CreateContext(dbName))
        {
            var item = Assert.Single(await CreateService(db).ListAsync(to));
            Assert.Equal(id, item.Id);
            Assert.Equal(to, item.Status);
        }
    }

    [Fact]
    public async Task List_WithoutAStatus_HidesArchivedFeedback()
    {
        await using var db = CreateContext();
        db.Feedback.AddRange(
            Feedback("new", FeedbackStatus.New),
            Feedback("reviewed", FeedbackStatus.Reviewed),
            Feedback("archived", FeedbackStatus.Archived));
        await db.SaveChangesAsync();

        var items = await CreateService(db).ListAsync();

        Assert.Equal(["new", "reviewed"], items.Select(i => i.Message).Order());
    }

    [Theory]
    [InlineData(FeedbackStatus.New, "new")]
    [InlineData(FeedbackStatus.Reviewed, "reviewed")]
    [InlineData(FeedbackStatus.Archived, "archived")]
    public async Task List_WithAStatus_ShowsOnlyFeedbackWithThatStatus(FeedbackStatus status, string expected)
    {
        await using var db = CreateContext();
        db.Feedback.AddRange(
            Feedback("new", FeedbackStatus.New),
            Feedback("reviewed", FeedbackStatus.Reviewed),
            Feedback("archived", FeedbackStatus.Archived));
        await db.SaveChangesAsync();

        var items = await CreateService(db).ListAsync(status);

        Assert.Equal(expected, Assert.Single(items).Message);
    }

    [Fact]
    public async Task ChangeStatus_UnknownFeedback_IsNotFound()
    {
        await using var db = CreateContext();

        Assert.False(await CreateService(db).ChangeStatusAsync(404, FeedbackStatus.Reviewed));
    }

    [Fact]
    public async Task ChangeStatus_SoftDeletedFeedback_IsNotFound_AndStaysUnchanged()
    {
        var dbName = $"FeedbackTriage_{Guid.NewGuid():N}";
        int id;
        await using (var db = CreateContext(dbName))
        {
            var deleted = Feedback("deleted");
            deleted.SoftDelete();
            db.Feedback.Add(deleted);
            await db.SaveChangesAsync();
            id = deleted.Id;
        }

        await using (var db = CreateContext(dbName))
        {
            Assert.False(await CreateService(db).ChangeStatusAsync(id, FeedbackStatus.Archived));
        }

        await using (var db = CreateContext(dbName))
        {
            var stored = await db.Feedback.IgnoreQueryFilters().SingleAsync(f => f.Id == id);
            Assert.Equal(FeedbackStatus.New, stored.Status);
        }
    }

    [Fact]
    public void AdminListQuery_DefaultInbox_TranslatesToAzureSql_ExcludingArchived()
    {
        using var db = AzureSqlTranslation.CreateContext();

        var sql = AzureSqlTranslation.AssertTranslates(CreateService(db).AdminListQuery());

        Assert.Contains("[f].[Status] <> 3", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminListQuery_WithAStatus_TranslatesToAzureSql()
    {
        using var db = AzureSqlTranslation.CreateContext();

        var sql = AzureSqlTranslation.AssertTranslates(CreateService(db).AdminListQuery(FeedbackStatus.Reviewed));

        Assert.Contains("[f].[Status] =", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY [f].[CreatedAt] DESC, [f].[Id] DESC", sql, StringComparison.Ordinal);
    }
}
