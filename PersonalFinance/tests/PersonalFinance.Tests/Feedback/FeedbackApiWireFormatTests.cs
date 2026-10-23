using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// Calls the real ApiService pipeline over HTTP through a Refit <see cref="IFeedbackApi"/> client with Refit's default
/// settings, as the Web app does, so the JSON the Web app sends is the JSON the ApiService has to read. Refit writes
/// enums as their names, so every Feedback enum must be readable by name on the ApiService side.
/// </summary>
[Collection("ApiServiceHost")]
public class FeedbackApiWireFormatTests : IClassFixture<FeedbackApiWireFormatTests.ApiServiceHost>
{
    private const string InternalKey = "feedback-wire-format-secret";

    private readonly ApiServiceHost _host;

    public FeedbackApiWireFormatTests(ApiServiceHost host)
    {
        _host = host;
    }

    [Theory]
    [InlineData(FeedbackCategory.Bug)]
    [InlineData(FeedbackCategory.Idea)]
    [InlineData(FeedbackCategory.Other)]
    public async Task SubmitThroughTheRefitContract_IsAccepted(FeedbackCategory category)
    {
        IFeedbackApi api = ApiFor($"wire-format-{Guid.NewGuid():N}");

        await AssertAcceptedAsync(() => api.SubmitAsync(Submission(category)));
    }

    [Fact]
    public async Task AdminListAndStatusChangeThroughTheRefitContract_RoundTripTheEnums()
    {
        IFeedbackApi admin = ApiFor(await CreateAdminAsync());
        string message = $"Wire format check {Guid.NewGuid():N}";
        await AssertAcceptedAsync(() => admin.SubmitAsync(Submission(FeedbackCategory.Idea, message)));

        FeedbackAdminItemDto listed = Assert.Single(await admin.ListAsync(), item => item.Message == message);
        Assert.Equal(FeedbackCategory.Idea, listed.Category);
        Assert.Equal(FeedbackStatus.New, listed.Status);

        await AssertAcceptedAsync(() =>
            admin.ChangeStatusAsync(listed.Id, new ChangeFeedbackStatusRequest { Status = FeedbackStatus.Archived }));

        FeedbackAdminItemDto archived = Assert.Single(await admin.ListAsync(FeedbackStatus.Archived), item => item.Id == listed.Id);
        Assert.Equal(FeedbackStatus.Archived, archived.Status);
    }

    private IFeedbackApi ApiFor(string userId)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);
        return RestService.For<IFeedbackApi>(client);
    }

    private async Task<string> CreateAdminAsync()
    {
        using IServiceScope scope = _host.Services.CreateScope();
        UserManager<IdentityUser> users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(AppRoles.Admin))
        {
            await roles.CreateAsync(new IdentityRole(AppRoles.Admin));
        }

        string email = $"admin-{Guid.NewGuid():N}@example.com";
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AppRoles.Admin)).Succeeded);
        return user.Id;
    }

    private static SubmitFeedbackRequest Submission(FeedbackCategory category,
        string message = "The Budget Tracker month picker is hard to find.") =>
        new() { Category = category, Message = message, PageUrl = "/BudgetTracker" };

    private static async Task AssertAcceptedAsync(Func<Task> call)
    {
        Exception? exception = await Record.ExceptionAsync(call);

        var apiException = exception as ApiException;
        Assert.True(exception is null,
            $"Expected success, got {apiException?.StatusCode.ToString() ?? exception?.GetType().Name}: {apiException?.Content}");
    }

    /// <summary>The ApiService entry point, InMemory database, shared secret configured.</summary>
    public sealed class ApiServiceHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                $"InMemory:FeedbackWireFormat_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", InternalKey);
        }
    }
}
