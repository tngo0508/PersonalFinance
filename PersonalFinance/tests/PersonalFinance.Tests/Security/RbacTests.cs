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
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Admin;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Security;

public class RbacTests
{
    private const string AdminEmail = "admin@example.com";

    private const string InternalKey = "test-internal-key";

    private static ServiceProvider BuildProvider(string? adminEmail = AdminEmail, bool withRoles = true, string? internalKey = InternalKey)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var settings = new Dictionary<string, string?>();
        if (adminEmail is not null)
        {
            settings[RbacSeeder.AdminEmailKey] = adminEmail;
        }

        if (internalKey is not null)
        {
            settings[XUserIdAuthenticationDefaults.InternalKeyConfig] = internalKey;
        }

        IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        services.AddSingleton<IConfiguration>(config);

        string dbName = $"RbacTest_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        IdentityBuilder builder = services.AddIdentityCore<IdentityUser>();
        if (withRoles)
        {
            builder.AddRoles<IdentityRole>();
        }

        builder.AddEntityFrameworkStores<AppDbContext>();

        services.AddAuthentication(XUserIdAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, XUserIdAuthenticationHandler>(
                XUserIdAuthenticationDefaults.Scheme, _ => { });

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }

    private static async Task<IdentityUser> AddUserAsync(IServiceProvider sp, string email, bool confirmed)
    {
        UserManager<IdentityUser> userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        return user;
    }

    [Fact]
    public async Task Seeder_CreatesAllRoles_AndIsIdempotent()
    {
        using ServiceProvider provider = BuildProvider(adminEmail: null);

        await RbacSeeder.SeedAsync(provider);
        await RbacSeeder.SeedAsync(provider);

        RoleManager<IdentityRole> roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (string role in AppRoles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(role));
        }

        Assert.Equal(AppRoles.All.Count, await roleManager.Roles.CountAsync());
    }

    [Fact]
    public async Task Seeder_GrantsAdmin_ToConfirmedConfiguredAccount()
    {
        using ServiceProvider provider = BuildProvider();
        IdentityUser user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        string? stampBefore = user.SecurityStamp;

        await RbacSeeder.SeedAsync(provider);

        IdentityUser reloaded = (await userManager.FindByIdAsync(user.Id))!;
        Assert.True(await userManager.IsInRoleAsync(reloaded, AppRoles.Admin));
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp);
    }

    [Fact]
    public async Task Seeder_DoesNotGrantAdmin_ToUnconfirmedAccount()
    {
        using ServiceProvider provider = BuildProvider();
        IdentityUser user = await AddUserAsync(provider, AdminEmail, confirmed: false);

        await RbacSeeder.SeedAsync(provider);

        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.False(await userManager.IsInRoleAsync(user, AppRoles.Admin));
    }

    [Fact]
    public async Task Seeder_IsNoOp_WhenConfigMissingOrAccountMissing()
    {
        using ServiceProvider noConfig = BuildProvider(adminEmail: null);
        IdentityUser account = await AddUserAsync(noConfig, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(noConfig);
        UserManager<IdentityUser> noConfigManager = noConfig.GetRequiredService<UserManager<IdentityUser>>();
        Assert.False(await noConfigManager.IsInRoleAsync(account, AppRoles.Admin));

        using ServiceProvider noAccount = BuildProvider();
        await RbacSeeder.SeedAsync(noAccount);
        Assert.Empty(await noAccount.GetRequiredService<AppDbContext>().UserRoles.ToListAsync());
    }

    [Fact]
    public async Task Seeder_BackfillsUserRole_ForExistingAccounts()
    {
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        IdentityUser user = await AddUserAsync(provider, "existing@example.com", confirmed: false);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();

        await RbacSeeder.SeedAsync(provider);
        await RbacSeeder.SeedAsync(provider);

        Assert.True(await userManager.IsInRoleAsync(user, AppRoles.User));
        Assert.False(await userManager.IsInRoleAsync(user, AppRoles.Admin));
    }

    [Fact]
    public async Task AssignDefaultRole_AddsUserRole_AndNeverThrowsWhenRoleMissing()
    {
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser first = await AddUserAsync(provider, "first@example.com", confirmed: true);

        await userManager.AssignDefaultRoleAsync(first);
        Assert.False(await userManager.IsInRoleAsync(first, AppRoles.User));

        await RbacSeeder.SeedAsync(provider);
        IdentityUser second = await AddUserAsync(provider, "second@example.com", confirmed: true);
        await userManager.AssignDefaultRoleAsync(second);
        Assert.True(await userManager.IsInRoleAsync(second, AppRoles.User));
    }

    [Fact]
    public async Task Seeder_NeverRemovesExistingAdmins()
    {
        using ServiceProvider provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        IdentityUser other = await AddUserAsync(provider, "other@example.com", confirmed: true);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        await userManager.AddToRoleAsync(other, AppRoles.Admin);
        await AddUserAsync(provider, AdminEmail, confirmed: true);

        await RbacSeeder.SeedAsync(provider);

        Assert.True(await userManager.IsInRoleAsync(other, AppRoles.Admin));
        Assert.Equal(2, (await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count);
    }

    [Fact]
    public async Task Seeder_SkipsGracefully_WhenRolesAreNotRegistered()
    {
        using ServiceProvider provider = BuildProvider(withRoles: false);
        await RbacSeeder.SeedAsync(provider);
    }

    private static async Task<ClaimsPrincipal?> AuthenticateAsync(ServiceProvider provider, string userId, string? rolesHeader = null, string? keyHeader = InternalKey)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers["X-User-Id"] = userId;
        if (keyHeader is not null)
        {
            context.Request.Headers[XUserIdAuthenticationDefaults.InternalKeyHeader] = keyHeader;
        }
        if (rolesHeader is not null)
        {
            context.Request.Headers["X-User-Roles"] = rolesHeader;
        }

        AuthenticateResult result = await context.AuthenticateAsync(XUserIdAuthenticationDefaults.Scheme);
        Assert.True(result.Succeeded);
        return result.Principal;
    }

    [Fact]
    public async Task AuthHandler_RejectsRequest_WhenInternalKeyConfiguredButMissingOrWrong()
    {
        using ServiceProvider provider = BuildProvider();
        IdentityUser user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(provider);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers["X-User-Id"] = user.Id;

        Assert.False((await context.AuthenticateAsync(XUserIdAuthenticationDefaults.Scheme)).Succeeded);

        context.Request.Headers[XUserIdAuthenticationDefaults.InternalKeyHeader] = "wrong";
        Assert.False((await context.AuthenticateAsync(XUserIdAuthenticationDefaults.Scheme)).Succeeded);
    }

    [Fact]
    public async Task AuthHandler_AttachesNoRoles_WhenNoInternalKeyConfiguredOutsideDevelopment()
    {
        using ServiceProvider provider = BuildProvider(internalKey: null);
        IdentityUser user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(provider);

        ClaimsPrincipal? principal = await AuthenticateAsync(provider, user.Id, keyHeader: null);

        Assert.True(principal!.Identity!.IsAuthenticated);
        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Seeder_DoesNotReAddUserRole_AfterItWasRemoved()
    {
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        IdentityUser user = await AddUserAsync(provider, "member@example.com", confirmed: true);
        UserManager<IdentityUser> userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        await RbacSeeder.SeedAsync(provider);
        await userManager.RemoveFromRoleAsync(user, AppRoles.User);

        await RbacSeeder.SeedAsync(provider);

        Assert.False(await userManager.IsInRoleAsync(user, AppRoles.User));
    }

    [Fact]
    public async Task AuthHandler_AddsRoleClaims_ForAdminAccount()
    {
        using ServiceProvider provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        IdentityUser user = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await RbacSeeder.SeedAsync(provider);

        ClaimsPrincipal? principal = await AuthenticateAsync(provider, user.Id);

        Assert.True(principal!.IsInRole(AppRoles.Admin));
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task AuthHandler_AddsNoRoleClaims_ForOrdinaryUnknownAndAnonymousIds_AndIgnoresSpoofedHeader()
    {
        using ServiceProvider provider = BuildProvider();
        IdentityUser ordinary = await AddUserAsync(provider, "user@example.com", confirmed: true);

        foreach (string? id in new[] { ordinary.Id, "does-not-exist", "anonymous" })
        {
            ClaimsPrincipal? principal = await AuthenticateAsync(provider, id, rolesHeader: "Admin");
            Assert.True(principal!.Identity!.IsAuthenticated);
            Assert.Empty(principal.FindAll(ClaimTypes.Role));
        }
    }

    [Fact]
    public async Task AdminService_CountsAccountsAndRoles()
    {
        using ServiceProvider provider = BuildProvider();
        await RbacSeeder.SeedAsync(provider);
        IdentityUser admin = await AddUserAsync(provider, AdminEmail, confirmed: true);
        await AddUserAsync(provider, "a@example.com", confirmed: true);
        await AddUserAsync(provider, "b@example.com", confirmed: false);
        await RbacSeeder.SeedAsync(provider);

        AdminSummaryDto summary = await CreateAdminService(provider).GetSummaryAsync();

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
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        AdminService service = CreateAdminService(provider);
        IdentityUser user = await AddUserAsync(provider, "a@example.com", confirmed: true);

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
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        AdminService service = CreateAdminService(provider);
        IdentityUser user = await AddUserAsync(provider, "a@example.com", confirmed: true);
        string? stampBefore = user.SecurityStamp;

        Assert.True((await service.AssignRoleAsync(user.Id, AppRoles.Admin)).Succeeded);
        Assert.True((await service.AssignRoleAsync(user.Id, AppRoles.Admin)).Succeeded);

        AdminUserDto listed = Assert.Single(await service.GetUsersAsync());
        Assert.Equal([AppRoles.Admin], listed.Roles);
        IdentityUser reloaded = (await provider.GetRequiredService<UserManager<IdentityUser>>().FindByIdAsync(user.Id))!;
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp);
        Assert.Equal(1, (await service.GetRolesAsync()).Single(r => r.Name == AppRoles.Admin).UserCount);
        Assert.Equal(AdminOperationStatus.NotFound, (await service.AssignRoleAsync("nope", AppRoles.Admin)).Status);
        Assert.Equal(AdminOperationStatus.NotFound, (await service.AssignRoleAsync(user.Id, "Missing")).Status);
    }

    [Fact]
    public async Task AdminService_RefusesSelfDemotionAndRemovingLastAdmin()
    {
        using ServiceProvider provider = BuildProvider(adminEmail: null);
        await RbacSeeder.SeedAsync(provider);
        AdminService service = CreateAdminService(provider);
        IdentityUser first = await AddUserAsync(provider, "a@example.com", confirmed: true);
        IdentityUser second = await AddUserAsync(provider, "b@example.com", confirmed: true);
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
        AuthorizeAttribute? attribute = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppPolicies.RequireAdmin, attribute!.Policy);
    }

    [Theory]
    [InlineData(typeof(PersonalFinance.Web.Controllers.AdminController), "Feedback")]
    [InlineData(typeof(PersonalFinance.ApiService.Features.Feedback.FeedbackController), "List")]
    [InlineData(typeof(PersonalFinance.Web.Controllers.AdminController), "ChangeFeedbackStatus")]
    [InlineData(typeof(PersonalFinance.ApiService.Features.Feedback.FeedbackController), "ChangeStatus")]
    public void AdminFeedbackInboxEndpoints_RequireTheAdminPolicy(Type controller, string action)
    {
        MethodInfo? method = controller.GetMethod(action);
        Assert.NotNull(method);

        IEnumerable<string?> policies = method!.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>())
            .Select(a => a.Policy);

        Assert.Contains(AppPolicies.RequireAdmin, policies);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static DefaultHttpContext ContextWithPolicy(string policy)
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
        DefaultHttpContext context = ContextWithPolicy(AppPolicies.RequireAdmin);
        bool nextCalled = false;
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
        DefaultHttpContext context = ContextWithPolicy("SomethingElse");
        bool nextCalled = false;
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
