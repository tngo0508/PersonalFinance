using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackApiController = PersonalFinance.ApiService.Controllers.FeedbackController;
using FeedbackEntity = PersonalFinance.Data.Entities.Feedback;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// The ApiService Feedback Status endpoint: a change returns 204, unknown Feedback returns 404 and an unknown
/// status returns 400.
/// </summary>
public class FeedbackStatusEndpointTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FeedbackStatusEndpoint_{Guid.NewGuid():N}")
            .Options);

    private static FeedbackApiController CreateController(AppDbContext db)
    {
        var service = new FeedbackService(db,
            new RateLimiterService(5, TimeSpan.FromHours(1)));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-user")], "TestAuthType");
        return new FeedbackApiController(service, new SubmitFeedbackRequestValidator(),
            new ChangeFeedbackStatusRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static async Task<int> AddFeedbackAsync(AppDbContext db)
    {
        var feedback = new FeedbackEntity { UserId = "user-1", Category = FeedbackCategory.Bug, Message = "Broken" };
        db.Feedback.Add(feedback);
        await db.SaveChangesAsync();
        return feedback.Id;
    }

    [Fact]
    public async Task ChangeStatus_KnownFeedback_ReturnsNoContent()
    {
        await using var db = CreateContext();
        var id = await AddFeedbackAsync(db);

        var result = await CreateController(db).ChangeStatus(id,
            new ChangeFeedbackStatusRequest { Status = FeedbackStatus.Reviewed });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ChangeStatus_UnknownFeedback_ReturnsNotFound()
    {
        await using var db = CreateContext();

        var result = await CreateController(db).ChangeStatus(999,
            new ChangeFeedbackStatusRequest { Status = FeedbackStatus.Reviewed });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ChangeStatus_UnknownStatus_ReturnsBadRequest_AndLeavesTheStatusUnchanged()
    {
        await using var db = CreateContext();
        var id = await AddFeedbackAsync(db);

        var result = await CreateController(db).ChangeStatus(id,
            new ChangeFeedbackStatusRequest { Status = (FeedbackStatus)99 });

        Assert.IsType<BadRequestObjectResult>(result);
        var item = Assert.Single(await new FeedbackService(db,
            new RateLimiterService(5, TimeSpan.FromHours(1))).ListAsync(FeedbackStatus.New));
        Assert.Equal(id, item.Id);
    }

    [Fact]
    public async Task List_UnknownStatus_ReturnsBadRequest()
    {
        await using var db = CreateContext();

        var result = await CreateController(db).List((FeedbackStatus)99);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
