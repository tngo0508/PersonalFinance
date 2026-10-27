using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Features.Feedback.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using FeedbackApiController = PersonalFinance.ApiService.Features.Feedback.FeedbackController;

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
        return new FeedbackApiController(service, new SubmitFeedbackRequestValidator(),
            new ChangeFeedbackStatusRequestValidator(), new FeedbackListFilterValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static ServiceProvider BuildProvider()
    {
        string databaseName = $"FeedbackDi_{Guid.NewGuid():N}";
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
    public async Task Submit_TheSixthWithinAnHour_Rejects429WithItsKey()
    {
        await using ServiceProvider provider = BuildProvider();
        for (int i = 0; i < 5; i++)
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            FeedbackApiController controller = CreateController(scope.ServiceProvider.GetRequiredService<IFeedbackService>());
            Assert.IsType<NoContentResult>(await controller.Submit(ValidRequest()));
        }

        await using (AsyncServiceScope sixth = provider.CreateAsyncScope())
        {
            FeedbackApiController controller = CreateController(sixth.ServiceProvider.GetRequiredService<IFeedbackService>());
            ApiErrorException error =
                await Assert.ThrowsAsync<ApiErrorException>(() => controller.Submit(ValidRequest()));
            Assert.Equal(StatusCodes.Status429TooManyRequests, error.StatusCode);
            Assert.Equal(ApiErrorKeys.Feedback.RateLimited, error.Key);
        }

        await using AsyncServiceScope check = provider.CreateAsyncScope();
        Assert.Equal(5, await check.ServiceProvider.GetRequiredService<AppDbContext>().Feedback.CountAsync());
    }

    [Fact]
    public void FeedbackLimiter_IsNotTheSharedRateLimiter()
    {
        using ServiceProvider provider = BuildProvider();

        IRateLimiterService shared = provider.GetRequiredService<IRateLimiterService>();
        IRateLimiterService feedback = provider.GetRequiredKeyedService<IRateLimiterService>(FeedbackRateLimit.Key);

        Assert.NotSame(shared, feedback);
    }
}
