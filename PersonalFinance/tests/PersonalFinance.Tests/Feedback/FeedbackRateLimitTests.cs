using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackApiController = PersonalFinance.ApiService.Controllers.FeedbackController;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// The Feedback submission limit at the ApiService endpoint and in DI: the 6th submission within an hour gets 429,
/// and the limiter is separate from the shared one other features use.
/// </summary>
public class FeedbackRateLimitTests
{
    private const string UserId = "feedback-user";

    private static FeedbackApiController CreateController(IFeedbackService service)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "TestAuthType");
        return new FeedbackApiController(service, new ChangeFeedbackStatusRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static ServiceProvider BuildProvider()
    {
        var databaseName = $"FeedbackDi_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IValidator<SubmitFeedbackRequest>, SubmitFeedbackRequestValidator>();
        services.AddSingleton<IRateLimiterService, RateLimiterService>();
        services.AddFeedbackFeature();
        return services.BuildServiceProvider();
    }

    private static SubmitFeedbackRequest ValidRequest() =>
        new() { Category = FeedbackCategory.Bug, Message = "The chart is blank." };

    [Fact]
    public async Task Submit_TheSixthWithinAnHour_Returns429WithAFriendlyMessage()
    {
        await using var provider = BuildProvider();
        IActionResult result = new NoContentResult();

        for (var i = 0; i < 6; i++)
        {
            await using var scope = provider.CreateAsyncScope();
            var controller = CreateController(scope.ServiceProvider.GetRequiredService<IFeedbackService>());
            result = await controller.Submit(ValidRequest());
        }

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
        Assert.Contains(SubmitFeedbackResult.RateLimitedMessage, objectResult.Value!.ToString());

        await using var check = provider.CreateAsyncScope();
        Assert.Equal(5, await check.ServiceProvider.GetRequiredService<AppDbContext>().Feedback.CountAsync());
    }

    [Fact]
    public void FeedbackLimiter_IsNotTheSharedRateLimiter()
    {
        using var provider = BuildProvider();

        var shared = provider.GetRequiredService<IRateLimiterService>();
        var feedback = provider.GetRequiredKeyedService<IRateLimiterService>(FeedbackService.RateLimiterKey);

        Assert.NotSame(shared, feedback);
    }
}
