using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Features.Feedback.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackApiController = PersonalFinance.ApiService.Features.Feedback.FeedbackController;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// The ApiService <c>POST /api/feedback</c> endpoint validates the request with
/// <see cref="SubmitFeedbackRequestValidator"/> before the service is called: invalid Feedback returns 400, is not
/// stored and does not count toward the rate limit.
/// </summary>
public class FeedbackSubmitEndpointTests
{
    private const string UserId = "feedback-user";

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FeedbackSubmitEndpoint_{Guid.NewGuid():N}")
            .Options);

    private static FeedbackApiController CreateController(AppDbContext db)
    {
        var service = new FeedbackService(db,
            new RateLimiterService(FeedbackRateLimit.MaxSubmissionsPerWindow, FeedbackRateLimit.Window));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "TestAuthType");
        return new FeedbackApiController(service, new SubmitFeedbackRequestValidator(),
            new ChangeFeedbackStatusRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static async Task<IEnumerable<string>> AssertBadRequestAsync(AppDbContext db,
        SubmitFeedbackRequest request)
    {
        IActionResult result = await CreateController(db).Submit(request);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.Feedback.ToListAsync());
        return Assert.IsType<ApiErrorDto>(badRequest.Value).Errors.Select(entry => entry.Key);
    }

    [Fact]
    public async Task Submit_ValidFeedback_ReturnsNoContent_AndStoresIt()
    {
        await using AppDbContext db = CreateContext();

        IActionResult result = await CreateController(db).Submit(new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Idea,
            Message = "Add CSV export.",
            PageUrl = "/Budget?month=10"
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("/Budget?month=10", Assert.Single(await db.Feedback.ToListAsync()).PageUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public async Task Submit_RejectsAnEmptyOrWhitespaceMessage(string message)
    {
        await using AppDbContext db = CreateContext();

        IEnumerable<string> errors = await AssertBadRequestAsync(db,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = message });

        Assert.Contains(ApiErrorKeys.Feedback.MessageRequired, errors);
    }

    [Fact]
    public async Task Submit_RejectsAMessageOverTheMaximumLength()
    {
        await using AppDbContext db = CreateContext();

        IEnumerable<string> errors = await AssertBadRequestAsync(db, new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Idea,
            Message = new string('a', 2001)
        });

        Assert.Contains(ApiErrorKeys.Feedback.MessageLength, errors);
    }

    [Fact]
    public async Task Submit_AcceptsAMessageOfExactlyTheMaximumLength()
    {
        await using AppDbContext db = CreateContext();

        IActionResult result = await CreateController(db).Submit(new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Other,
            Message = new string('a', 2000)
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Single(await db.Feedback.ToListAsync());
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example")]
    [InlineData("Dashboard")]
    public async Task Submit_RejectsAPageThatIsNotAnAppRelativePath(string pageUrl)
    {
        await using AppDbContext db = CreateContext();

        IEnumerable<string> errors = await AssertBadRequestAsync(db,
            new SubmitFeedbackRequest { Category = FeedbackCategory.Bug, Message = "Hi", PageUrl = pageUrl });

        Assert.Contains(ApiErrorKeys.Feedback.PageNotAppRelative, errors);
    }

    [Fact]
    public async Task Submit_RejectsAPageOverTheMaximumLength()
    {
        await using AppDbContext db = CreateContext();

        IEnumerable<string> errors = await AssertBadRequestAsync(db, new SubmitFeedbackRequest
        {
            Category = FeedbackCategory.Bug,
            Message = "Hi",
            PageUrl = "/" + new string('a', 500)
        });

        Assert.Contains(ApiErrorKeys.Feedback.PageTooLong, errors);
    }

    [Fact]
    public async Task Submit_RejectsAnUnknownCategory()
    {
        await using AppDbContext db = CreateContext();

        IEnumerable<string> errors = await AssertBadRequestAsync(db,
            new SubmitFeedbackRequest { Category = (FeedbackCategory)99, Message = "Hi" });

        Assert.Contains(ApiErrorKeys.Feedback.CategoryUnknown, errors);
    }

    [Fact]
    public async Task Submit_InvalidFeedback_DoesNotUseUpTheLimit()
    {
        await using AppDbContext db = CreateContext();
        FeedbackApiController controller = CreateController(db);
        for (int i = 0; i < FeedbackRateLimit.MaxSubmissionsPerWindow; i++)
        {
            await controller.Submit(new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "" });
        }

        IActionResult result = await controller.Submit(
            new SubmitFeedbackRequest { Category = FeedbackCategory.Idea, Message = "Add CSV export." });

        Assert.IsType<NoContentResult>(result);
    }
}
