using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Behaviour of the Admin Feedback inbox on <see cref="IFeedbackService"/>, against an in-memory database.
/// </summary>
public class FeedbackInboxServiceTests
{
    private static readonly DateTime Monday = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FeedbackInbox_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static FeedbackService CreateService(AppDbContext db) =>
        new(db, new RateLimiterService(5, TimeSpan.FromHours(1)));

    private static FeedbackEntity Feedback(string message, DateTime createdAt, string userId = "user-1") => new()
    {
        UserId = userId,
        Category = FeedbackCategory.Idea,
        Message = message,
        CreatedAt = createdAt
    };

    [Fact]
    public async Task List_ShowsNewestFirst()
    {
        await using AppDbContext db = CreateContext();
        db.Feedback.AddRange(
            Feedback("oldest", Monday),
            Feedback("newest", Monday.AddDays(2)),
            Feedback("middle", Monday.AddDays(1)));
        await db.SaveChangesAsync();

        List<FeedbackAdminItemDto> items = await CreateService(db).ListAsync();

        Assert.Equal(["newest", "middle", "oldest"], items.Select(i => i.Message));
    }

    [Fact]
    public async Task List_SentAtTheSameMoment_PutsTheLaterOneFirst()
    {
        await using AppDbContext db = CreateContext();
        db.Feedback.Add(Feedback("first sent", Monday));
        await db.SaveChangesAsync();
        db.Feedback.Add(Feedback("second sent", Monday));
        await db.SaveChangesAsync();

        List<FeedbackAdminItemDto> items = await CreateService(db).ListAsync();

        Assert.Equal(["second sent", "first sent"], items.Select(i => i.Message));
    }

    [Fact]
    public async Task List_LeavesOutSoftDeletedFeedback()
    {
        await using AppDbContext db = CreateContext();
        FeedbackEntity deleted = Feedback("deleted", Monday);
        deleted.SoftDelete();
        db.Feedback.AddRange(deleted, Feedback("kept", Monday));
        await db.SaveChangesAsync();

        List<FeedbackAdminItemDto> items = await CreateService(db).ListAsync();

        Assert.Equal("kept", Assert.Single(items).Message);
    }

    [Fact]
    public async Task List_ShowsCategoryMessageAccountEmailPageStatusAndDate()
    {
        await using AppDbContext db = CreateContext();
        db.Users.Add(new IdentityUser { Id = "user-1", UserName = "ana@example.com", Email = "ana@example.com" });
        db.Feedback.Add(new FeedbackEntity
        {
            UserId = "user-1",
            Category = FeedbackCategory.Bug,
            Message = "The chart is blank.",
            PageUrl = "/Dashboard",
            CreatedAt = Monday
        });
        await db.SaveChangesAsync();

        FeedbackAdminItemDto item = Assert.Single(await CreateService(db).ListAsync());

        Assert.Equal(FeedbackCategory.Bug, item.Category);
        Assert.Equal("The chart is blank.", item.Message);
        Assert.Equal("ana@example.com", item.AccountEmail);
        Assert.Equal("/Dashboard", item.PageUrl);
        Assert.Equal(FeedbackStatus.New, item.Status);
        Assert.Equal(Monday, item.CreatedAt);
        Assert.True(item.Id > 0);
    }

    [Fact]
    public async Task List_FeedbackFromAnAccountThatNoLongerExists_IsStillListed_WithoutAnEmail()
    {
        await using AppDbContext db = CreateContext();
        db.Feedback.Add(Feedback("orphan", Monday, userId: "gone"));
        await db.SaveChangesAsync();

        FeedbackAdminItemDto item = Assert.Single(await CreateService(db).ListAsync());

        Assert.Equal(string.Empty, item.AccountEmail);
    }

    [Fact]
    public async Task List_WhenThereIsNoFeedback_IsEmpty()
    {
        await using AppDbContext db = CreateContext();

        Assert.Empty(await CreateService(db).ListAsync());
    }

    [Fact]
    public void AdminListQuery_TranslatesToAzureSql_AsOneOrderedQuery()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(CreateService(db).AdminListQuery());

        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains("[IsDeleted]", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY [f].[CreatedAt] DESC, [f].[Id] DESC", sql, StringComparison.Ordinal);
    }
}
