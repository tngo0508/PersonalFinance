using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Infrastructure;
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

    private static AppDbContext CreateContext() => CreateContext($"Feedback_{Guid.NewGuid():N}");

    private static AppDbContext CreateContext(string databaseName, params IInterceptor[] interceptors)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    private static FeedbackService CreateService(AppDbContext db) =>
        CreateService(db, new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window));

    private static FeedbackService CreateService(AppDbContext db, IRateLimiterService rateLimiter) =>
        new FeedbackService(db, rateLimiter, TimeProvider.System);

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
    public async Task Submit_WhenTheSaveFails_DoesNotCountTowardTheLimit()
    {
        string databaseName = $"Feedback_{Guid.NewGuid():N}";
        var rateLimiter = new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window);
        await using (AppDbContext failing = CreateContext(databaseName, new FailingSaveInterceptor()))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                CreateService(failing, rateLimiter).SubmitAsync(UserId, ValidRequest()));
        }

        await using AppDbContext db = CreateContext(databaseName);
        FeedbackService service = CreateService(db, rateLimiter);
        for (int i = 0; i < 5; i++)
        {
            Assert.True((await service.SubmitAsync(UserId, ValidRequest())).Succeeded);
        }

        SubmitFeedbackResult sixth = await service.SubmitAsync(UserId, ValidRequest());

        Assert.True(sixth.RateLimited);
        Assert.Equal(5, await db.Feedback.CountAsync());
    }

    [Fact]
    public async Task Submit_ConcurrentlyFromOneAccount_StoresNoMoreThanTheLimit()
    {
        string databaseName = $"Feedback_{Guid.NewGuid():N}";
        var rateLimiter = new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window);

        SubmitFeedbackResult[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using AppDbContext db = CreateContext(databaseName);
            return await CreateService(db, rateLimiter).SubmitAsync(UserId, ValidRequest());
        }));

        Assert.Equal(5, results.Count(r => r.Succeeded));
        Assert.Equal(5, results.Count(r => r.RateLimited));
        await using AppDbContext check = CreateContext(databaseName);
        Assert.Equal(5, await check.Feedback.CountAsync());
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

    /// <summary>Makes every save on its context fail, the way a lost database connection would.</summary>
    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("The database is unavailable.");
    }
}
