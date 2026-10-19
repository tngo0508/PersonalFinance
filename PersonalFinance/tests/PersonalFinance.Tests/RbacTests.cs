using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests;

public class RbacTests
{
    private const string AdminEmail = "admin@example.com";

    private static ServiceProvider BuildProvider(string? adminEmail = AdminEmail, bool withRoles = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(adminEmail is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { [RbacSeeder.AdminEmailKey] = adminEmail })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        var dbName = $"RbacTest_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var builder = services.AddIdentityCore<IdentityUser>();
        if (withRoles)
        {
            builder.AddRoles<IdentityRole>();
        }

        builder.AddEntityFrameworkStores<AppDbContext>();

        services.AddAuthentication(XUserIdAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, XUserIdAuthenticationHandler>(
                XUserIdAuthenticationDefaults.Scheme, _ => { });

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }

    private static async Task<IdentityUser> AddUserAsync(IServiceProvider sp, string email, bool confirmed)
    {
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        return user;
    }

    [Fact]
    public async Task Seeder_CreatesAllRoles_AndIsIdempotent()
    {
        using var provider = BuildProvider(adminEmail: null);

        await RbacSeeder.SeedAsync(provider);
        await RbacSeeder.SeedAsync(provider);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(role));
        }

        Assert.Equal(AppRoles.All.Count, await roleManager.Roles.CountAsync());
    }

    [Fact]
    public async Task Seeder_GrantsAdmin_ToConfirmedConfiguredAccount()
    {
        using var provider = BuildProvider();
        var user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var stampBefore = user.SecurityStamp;

        await RbacSeeder.SeedAsync(provider);

        var reloaded = (await userManager.FindByIdAsync(user.Id))!;
        Assert.True(await userManager.IsInRoleAsync(reloaded, AppRoles.Admin));
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp);
    }

    [Fact]
    public async Task Seeder_DoesNotGrantAdmin_ToUnconfirmedAccount()
    {
        using var provider = BuildProvider();
        var user = await AddUserAsync(provider, AdminEmail, confirmed: false);

        await RbacSeeder.SeedAsync(provider);

        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.False(await userManager.IsInRoleAsync(user, AppRoles.Admin));
    }

    [Fact]
    public async Task Seeder_IsNoOp_WhenConfigMissingOrAccountMissing()
    {
        using var noConfig = BuildProvider(adminEmail: null);
        await AddUserAsync(noConfig, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(noConfig);
        Assert.Empty(await noConfig.GetRequiredService<AppDbContext>().UserRoles.ToListAsync());

        using var noAccount = BuildProvider();
        await RbacSeeder.SeedAsync(noAccount);
        Assert.Empty(await noAccount.GetRequiredService<AppDbContext>().UserRoles.ToListAsync());
    }

    [Fact]
    public async Task Seeder_NeverRemovesExistingAdmins()
    {
        using var provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        var other = await AddUserAsync(provider, "other@example.com", confirmed: true);
        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        await userManager.AddToRoleAsync(other, AppRoles.Admin);
        await AddUserAsync(provider, AdminEmail, confirmed: true);

        await RbacSeeder.SeedAsync(provider);

        Assert.True(await userManager.IsInRoleAsync(other, AppRoles.Admin));
        Assert.Equal(2, (await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count);
    }

    [Fact]
    public async Task Seeder_SkipsGracefully_WhenRolesAreNotRegistered()
    {
        using var provider = BuildProvider(withRoles: false);
        await RbacSeeder.SeedAsync(provider);
    }

    private static async Task<ClaimsPrincipal?> AuthenticateAsync(ServiceProvider provider, string userId, string? rolesHeader = null)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers["X-User-Id"] = userId;
        if (rolesHeader is not null)
        {
            context.Request.Headers["X-User-Roles"] = rolesHeader;
        }

        var result = await context.AuthenticateAsync(XUserIdAuthenticationDefaults.Scheme);
        Assert.True(result.Succeeded);
        return result.Principal;
    }

    [Fact]
    public async Task AuthHandler_AddsRoleClaims_ForAdminAccount()
    {
        using var provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        var user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(provider);

        var principal = await AuthenticateAsync(provider, user.Id);

        Assert.True(principal!.IsInRole(AppRoles.Admin));
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task AuthHandler_AddsNoRoleClaims_ForOrdinaryUnknownAndAnonymousIds_AndIgnoresSpoofedHeader()
    {
        using var provider = BuildProvider();
        var ordinary = await AddUserAsync(provider, "user@example.com", confirmed: true);

        foreach (var id in new[] { ordinary.Id, "does-not-exist", "anonymous" })
        {
            var principal = await AuthenticateAsync(provider, id, rolesHeader: "Admin");
            Assert.True(principal!.Identity!.IsAuthenticated);
            Assert.Empty(principal.FindAll(ClaimTypes.Role));
        }
    }

    [Fact]
    public async Task AdminService_CountsAccountsAndRoles()
    {
        using var provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        var admin = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await AddUserAsync(provider, "a@example.com", confirmed: true);
        await AddUserAsync(provider, "b@example.com", confirmed: false);
        await RbacSeeder.SeedAsync(provider);

        var summary = await CreateAdminService(provider).GetSummaryAsync();

        Assert.Equal(3, summary.TotalAccounts);
        Assert.Equal(2, summary.ConfirmedAccounts);
        Assert.Equal(1, summary.UnconfirmedAccounts);
        Assert.Equal(1, summary.AccountsPerRole[AppRoles.Admin]);
        Assert.NotNull(admin);
    }

    private static AdminService CreateAdminService(IServiceProvider provider) => new(
        provider.GetRequiredService<AppDbContext>(),
        provider.GetRequiredService<UserManager<IdentityUser>>(),
        provider.GetRequiredService<RoleManager<IdentityRole>>());

    [Fact]
    public async Task AdminService_CreatesAndDeletesCustomRole_ButNotBuiltInOrNonEmpty()
    {
        using var provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        var service = CreateAdminService(provider);
        var user = await AddUserAsync(provider, "a@example.com", confirmed: true);

        Assert.True((await service.CreateRoleAsync("ReadOnly")).Succeeded);
        Assert.False((await service.CreateRoleAsync("readonly")).Succeeded);
        Assert.False((await service.CreateRoleAsync("bad name!")).Succeeded);
        Assert.False((await service.DeleteRoleAsync(AppRoles.Admin)).Succeeded);

        Assert.True((await service.AssignRoleAsync(user.Id, "ReadOnly")).Succeeded);
        Assert.False((await service.DeleteRoleAsync("ReadOnly")).Succeeded);

        Assert.True((await service.RemoveRoleAsync("someone-else", user.Id, "ReadOnly")).Succeeded);
        Assert.True((await service.DeleteRoleAsync("ReadOnly")).Succeeded);
        Assert.Equal(AdminOperationStatus.NotFound, (await service.DeleteRoleAsync("ReadOnly")).Status);
    }

    [Fact]
    public async Task AdminService_AssignsRole_UpdatesStampAndListsIt()
    {
        using var provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        var service = CreateAdminService(provider);
        var user = await AddUserAsync(provider, "a@example.com", confirmed: true);
        var stampBefore = user.SecurityStamp;

        Assert.True((await service.AssignRoleAsync(user.Id, AppRoles.Admin)).Succeeded);
        Assert.True((await service.AssignRoleAsync(user.Id, AppRoles.Admin)).Succeeded);

        var listed = Assert.Single(await service.GetUsersAsync());
        Assert.Equal([AppRoles.Admin], listed.Roles);
        var reloaded = (await provider.GetRequiredService<UserManager<IdentityUser>>().FindByIdAsync(user.Id))!;
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp);
        Assert.Equal(1, Assert.Single(await service.GetRolesAsync()).UserCount);
        Assert.Equal(AdminOperationStatus.NotFound, (await service.AssignRoleAsync("nope", AppRoles.Admin)).Status);
        Assert.Equal(AdminOperationStatus.NotFound, (await service.AssignRoleAsync(user.Id, "Missing")).Status);
    }

    [Fact]
    public async Task AdminService_RefusesSelfDemotionAndRemovingLastAdmin()
    {
        using var provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        var service = CreateAdminService(provider);
        var first = await AddUserAsync(provider, "a@example.com", confirmed: true);
        var second = await AddUserAsync(provider, "b@example.com", confirmed: true);
        await service.AssignRoleAsync(first.Id, AppRoles.Admin);

        Assert.False((await service.RemoveRoleAsync(first.Id, first.Id, AppRoles.Admin)).Succeeded);
        Assert.False((await service.RemoveRoleAsync(second.Id, first.Id, AppRoles.Admin)).Succeeded);

        await service.AssignRoleAsync(second.Id, AppRoles.Admin);
        Assert.True((await service.RemoveRoleAsync(second.Id, first.Id, AppRoles.Admin)).Succeeded);
    }

    [Theory]
    [InlineData(typeof(PersonalFinance.Web.Controllers.AdminController))]
    [InlineData(typeof(PersonalFinance.ApiService.Controllers.AdminController))]
    public void AdminControllers_CarryTheRequireAdminPolicy(Type controller)
    {
        var attribute = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppPolicies.RequireAdmin, attribute!.Policy);
    }

    private static HttpContext ContextWithPolicy(string policy)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { Policy = policy }),
            "test"));
        return context;
    }

    [Fact]
    public async Task ResultHandler_Returns404_WhenAdminPolicyForbidden()
    {
        var context = ContextWithPolicy(AppPolicies.RequireAdmin);
        var nextCalled = false;
        var handler = new AdminNotFoundAuthorizationResultHandler();

        await handler.HandleAsync(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid());

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task ResultHandler_DelegatesToDefault_ForOtherPoliciesAndSuccess()
    {
        var context = ContextWithPolicy("SomethingElse");
        var nextCalled = false;
        var handler = new AdminNotFoundAuthorizationResultHandler();

        await handler.HandleAsync(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Success());

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }
}
