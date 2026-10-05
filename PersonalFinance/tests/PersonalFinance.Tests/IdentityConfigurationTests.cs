using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalFinance.Data;
using Xunit;

namespace PersonalFinance.Tests;

public class IdentityConfigurationTests
{
    private static ServiceProvider BuildIdentityServiceProvider(bool requireConfirmedAccount = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext, SqliteAppDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        services.AddDefaultIdentity<IdentityUser>(options =>
            options.SignIn.RequireConfirmedAccount = requireConfirmedAccount)
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<AppDbContext>();
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();

        return provider;
    }

    [Fact]
    public void IdentityOptions_RequireConfirmedAccount_IsConfiguredAsTrue()
    {
        using var provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.SignIn.RequireConfirmedAccount);
    }

    [Fact]
    public async Task SignInManager_CanSignInAsync_RejectsUnconfirmedUserWhenRequireConfirmedAccountIsTrue()
    {
        using var provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var unconfirmedUser = new IdentityUser
        {
            UserName = "spam_bot@example.com",
            Email = "spam_bot@example.com",
            EmailConfirmed = false
        };

        var createResult = await userManager.CreateAsync(unconfirmedUser, "P@ssword123!");
        Assert.True(createResult.Succeeded);

        var canSignIn = await signInManager.CanSignInAsync(unconfirmedUser);
        Assert.False(canSignIn);
    }

    [Fact]
    public async Task SignInManager_CanSignInAsync_AllowsConfirmedUserWhenRequireConfirmedAccountIsTrue()
    {
        using var provider = BuildIdentityServiceProvider(requireConfirmedAccount: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var confirmedUser = new IdentityUser
        {
            UserName = "verified_user@example.com",
            Email = "verified_user@example.com",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(confirmedUser, "P@ssword123!");
        Assert.True(createResult.Succeeded);

        var canSignIn = await signInManager.CanSignInAsync(confirmedUser);
        Assert.True(canSignIn);
    }
}
