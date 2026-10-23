using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Behaviour of <see cref="IFeedbackService"/> when an Account sends already-validated Feedback, against an
/// in-memory database. Request validation is the controller's job (see <see cref="FeedbackSubmitEndpointTests"/>).
/// </summary>
public class FeedbackServiceTests
{
    private const string UserId = "feedback-user";

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Feedback_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static FeedbackService CreateService(AppDbContext db) =>
        CreateService(db, new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window));

    private static FeedbackService CreateService(AppDbContext db, IRateLimiterService rateLimiter) =>
        new FeedbackService(db, rateLimiter);

    private static SubmitFeedbackRequest ValidRequest() =>
        new() { Category = FeedbackCategory.Idea, Message = "Add CSV export." };

    [Fact]
    public async Task Submit_TheSixthWithinAnHour_IsRateLimited_AndNotStored()
    {
        await using AppDbContext db = CreateContext();
        FeedbackService service = CreateService(db);

        for (int i = 0; i < 5; i++)
        {
            Assert.True((await service.SubmitAsync(UserId, ValidRequest())).Succeeded);
        }

        SubmitFeedbackResult sixth = await service.SubmitAsync(UserId, ValidRequest());

        Assert.False(sixth.Succeeded);
        Assert.True(sixth.RateLimited);
        Assert.Equal(5, await db.Feedback.CountAsync());
    }

    [Fact]
    public async Task Submit_AnotherAccount_IsNotAffectedByTheLimit()
    {
        await using AppDbContext db = CreateContext();
        FeedbackService service = CreateService(db);
        for (int i = 0; i < 5; i++)
        {
            await service.SubmitAsync(UserId, ValidRequest());
        }

        SubmitFeedbackResult other = await service.SubmitAsync("other-user", ValidRequest());

        Assert.True(other.Succeeded);
        Assert.Equal(6, await db.Feedback.CountAsync());
    }

    [Fact]
    public async Task Submit_WhenTheWindowHasPassed_IsAcceptedAgain()
    {
        await using AppDbContext db = CreateContext();
        FeedbackService service = CreateService(db, new RateLimiterService(5, TimeSpan.FromMilliseconds(50)));
        for (int i = 0; i < 5; i++)
        {
            await service.SubmitAsync(UserId, ValidRequest());
        }

        await Task.Delay(120);
        SubmitFeedbackResult result = await service.SubmitAsync(UserId, ValidRequest());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Submit_StoresFeedbackForTheAccount_WithStatusNew()
    {
        await using AppDbContext db = CreateContext();

        SubmitFeedbackResult result = await CreateService(db).SubmitAsync(UserId, new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Bug,
            Message = "The chart on the Dashboard is blank.",
            PageUrl = "/Dashboard"
        });

        Assert.True(result.Succeeded);
        Data.Entities.Feedback stored = Assert.Single(await db.Feedback.ToListAsync());
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal(FeedbackStatus.New, stored.Status);
        Assert.Equal(FeedbackCategory.Bug, stored.Category);
        Assert.Equal("The chart on the Dashboard is blank.", stored.Message);
        Assert.Equal("/Dashboard", stored.PageUrl);
    }

    [Fact]
    public async Task Submit_WithoutAPage_IsAccepted()
    {
        await using AppDbContext db = CreateContext();

        SubmitFeedbackResult result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "Dark mode, please." });

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(await db.Feedback.ToListAsync()).PageUrl);
    }

    [Fact]
    public async Task Submit_TrimsTheMessage()
    {
        await using AppDbContext db = CreateContext();

        await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "  Add CSV export.  " });

        Assert.Equal("Add CSV export.", Assert.Single(await db.Feedback.ToListAsync()).Message);
    }
}
