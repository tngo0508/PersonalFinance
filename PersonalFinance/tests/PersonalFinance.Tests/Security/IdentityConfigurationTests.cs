using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using Xunit;

namespace PersonalFinance.Tests.Security;

public class IdentityConfigurationTests
{
    private static ServiceProvider BuildIdentityServiceProvider(bool requireConfirmedAccount = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"IdentityTest_{Guid.NewGuid():N}"));

        services.AddDefaultIdentity<IdentityUser>(options =>
            options.SignIn.RequireConfirmedAccount = requireConfirmedAccount)
            .AddEntityFrameworkStores<AppDbContext>();

        ServiceProvider provider = services.BuildServiceProvider();
        AppDbContext dbContext = provider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();

        return provider;
    }

    [Fact]
    public void IdentityOptions_RequireConfirmedAccount_IsConfiguredAsTrue()
    {
        using ServiceProvider provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        IdentityOptions options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.SignIn.RequireConfirmedAccount);
    }

    [Fact]
    public async Task SignInManager_CanSignInAsync_RejectsUnconfirmedUserWhenRequireConfirmedAccountIsTrue()
    {
        using ServiceProvider provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        SignInManager<IdentityUser> signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var unconfirmedUser = new IdentityUser
        {
            UserName = "spam_bot@example.com",
            Email = "spam_bot@example.com",
            EmailConfirmed = false
        };

        IdentityResult createResult = await userManager.CreateAsync(unconfirmedUser, "P@ssword123!");
        Assert.True(createResult.Succeeded);

        bool canSignIn = await signInManager.CanSignInAsync(unconfirmedUser);
        Assert.False(canSignIn);
    }

    [Fact]
    public async Task SignInManager_CanSignInAsync_AllowsConfirmedUserWhenRequireConfirmedAccountIsTrue()
    {
        using ServiceProvider provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        SignInManager<IdentityUser> signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var confirmedUser = new IdentityUser
        {
            UserName = "verified_user@example.com",
            Email = "verified_user@example.com",
            EmailConfirmed = true
        };

        IdentityResult createResult = await userManager.CreateAsync(confirmedUser, "P@ssword123!");
        Assert.True(createResult.Succeeded);

        bool canSignIn = await signInManager.CanSignInAsync(confirmedUser);
        Assert.True(canSignIn);
    }
}
