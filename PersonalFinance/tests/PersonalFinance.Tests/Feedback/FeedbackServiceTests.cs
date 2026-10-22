using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Behaviour of <see cref="IFeedbackService"/> when an Account sends Feedback, against an in-memory database.
/// </summary>
public class FeedbackServiceTests
{
    private const string UserId = "feedback-user";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Feedback_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static IFeedbackService CreateService(AppDbContext db) =>
        CreateService(db, FeedbackService.CreateRateLimiter());

    private static IFeedbackService CreateService(AppDbContext db, IRateLimiterService rateLimiter) =>
        new FeedbackService(db, new SubmitFeedbackRequestValidator(), rateLimiter);

    private static SubmitFeedbackRequest ValidRequest() =>
        new() { Category = FeedbackCategory.Idea, Message = "Add CSV export." };

    [Fact]
    public async Task Submit_TheSixthWithinAnHour_IsRateLimited_AndNotStored()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        for (var i = 0; i < 5; i++)
        {
            Assert.True((await service.SubmitAsync(UserId, ValidRequest())).Succeeded);
        }

        var sixth = await service.SubmitAsync(UserId, ValidRequest());

        Assert.False(sixth.Succeeded);
        Assert.True(sixth.RateLimited);
        Assert.Contains(SubmitFeedbackResult.RateLimitedMessage, sixth.Errors);
        Assert.Equal(5, await db.Feedback.CountAsync());
    }

    [Fact]
    public async Task Submit_AnotherAccount_IsNotAffectedByTheLimit()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        for (var i = 0; i < 5; i++)
        {
            await service.SubmitAsync(UserId, ValidRequest());
        }

        var other = await service.SubmitAsync("other-user", ValidRequest());

        Assert.True(other.Succeeded);
        Assert.Equal(6, await db.Feedback.CountAsync());
    }

    [Fact]
    public async Task Submit_InvalidFeedback_DoesNotUseUpTheLimit()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        for (var i = 0; i < 5; i++)
        {
            await service.SubmitAsync(UserId, new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "" });
        }

        var result = await service.SubmitAsync(UserId, ValidRequest());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Submit_WhenTheWindowHasPassed_IsAcceptedAgain()
    {
        await using var db = CreateContext();
        var service = CreateService(db, new RateLimiterService(5, TimeSpan.FromMilliseconds(50)));
        for (var i = 0; i < 5; i++)
        {
            await service.SubmitAsync(UserId, ValidRequest());
        }

        await Task.Delay(120);
        var result = await service.SubmitAsync(UserId, ValidRequest());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Submit_StoresFeedbackForTheAccount_WithStatusNew()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId, new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Bug,
            Message = "The chart on the Dashboard is blank.",
            PageUrl = "/Dashboard"
        });

        Assert.True(result.Succeeded);
        var stored = Assert.Single(await db.Feedback.ToListAsync());
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal(FeedbackStatus.New, stored.Status);
        Assert.Equal(FeedbackCategory.Bug, stored.Category);
        Assert.Equal("The chart on the Dashboard is blank.", stored.Message);
        Assert.Equal("/Dashboard", stored.PageUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_RejectsAnEmptyMessage_AndStoresNothing(string message)
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = message });

        Assert.False(result.Succeeded);
        Assert.Contains("Message is required.", result.Errors);
        Assert.Empty(await db.Feedback.ToListAsync());
    }

    [Fact]
    public async Task Submit_RejectsAMessageOver2000Characters()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = new string('a', 2001) });

        Assert.False(result.Succeeded);
        Assert.Contains("Message must be 2,000 characters or fewer.", result.Errors);
        Assert.Empty(await db.Feedback.ToListAsync());
    }

    [Fact]
    public async Task Submit_AcceptsAMessageOfExactly2000Characters()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Other, Message = new string('a', 2000) });

        Assert.True(result.Succeeded);
        Assert.Single(await db.Feedback.ToListAsync());
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example")]
    [InlineData("Dashboard")]
    public async Task Submit_RejectsAPageThatIsNotAnAppRelativePath(string pageUrl)
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Bug, Message = "Hi", PageUrl = pageUrl });

        Assert.False(result.Succeeded);
        Assert.Contains("Page must be a path within the app.", result.Errors);
    }

    [Fact]
    public async Task Submit_RejectsAPageOver500Characters()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId, new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Bug, Message = "Hi", PageUrl = "/" + new string('a', 500)
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Submit_WithoutAPage_IsAccepted()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "Dark mode, please." });

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(await db.Feedback.ToListAsync()).PageUrl);
    }

    [Fact]
    public async Task Submit_RejectsAnUnknownCategory()
    {
        await using var db = CreateContext();

        var result = await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = (FeedbackCategory)99, Message = "Hi" });

        Assert.False(result.Succeeded);
        Assert.Contains("Choose a category: Bug, Idea or Other.", result.Errors);
    }

    [Fact]
    public async Task Submit_TrimsTheMessage()
    {
        await using var db = CreateContext();

        await CreateService(db).SubmitAsync(UserId,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "  Add CSV export.  " });

        Assert.Equal("Add CSV export.", Assert.Single(await db.Feedback.ToListAsync()).Message);
    }
}
