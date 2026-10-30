using Microsoft.AspNetCore.Identity;
using PersonalFinance.Data;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Creates the Atelier E2E smoke's fixture Account at startup: the learner whose Lesson 1 the ApiService's
/// <c>LearningFixtureSeeder</c> stores as finished two days ago, so the smoke can open Lesson 2 and its Review (#520).
/// The two share <see cref="UserId"/>. On only when the host is Development and <c>E2E_SEED_LEARNING=true</c>; never in
/// Production. The email and password are test values the smoke signs in with.
/// </summary>
internal static class LearningFixtureAccountSeeder
{
    /// <summary>The setting (an environment variable in CI) that turns the fixture on.</summary>
    public const string Setting = "E2E_SEED_LEARNING";

    /// <summary>The Account's Identity id, which the Web forwards to ApiService as the user id.</summary>
    public const string UserId = "e2e-atelier-learner";

    /// <summary>The Account's email, which e2e-tests/learning-smoke.js signs in with.</summary>
    public const string Email = "e2e-atelier-learner@example.test";

    /// <summary>The Account's test password, which e2e-tests/learning-smoke.js signs in with.</summary>
    public const string Password = "Atelier-E2E-2026!";

    /// <summary>True only in Development with <see cref="Setting"/> set to <c>true</c>.</summary>
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        && string.Equals(configuration[Setting], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Migrates, then creates the Account, when <see cref="IsEnabled"/>.</summary>
    public static async Task SeedIfEnabledAsync(WebApplication app)
    {
        if (!IsEnabled(app.Environment, app.Configuration))
        {
            return;
        }

        await app.MigrateAndSeedDatabaseAsync();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>());
        app.Logger.LogWarning("{Setting} is on: created the Atelier fixture Account for E2E tests.", Setting);
    }

    /// <summary>Creates the confirmed Account unless it exists.</summary>
    internal static async Task SeedAsync(UserManager<IdentityUser> userManager)
    {
        if (await userManager.FindByIdAsync(UserId) is not null)
        {
            return;
        }

        var user = new IdentityUser { Id = UserId, UserName = Email, Email = Email, EmailConfirmed = true };
        IdentityResult result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the Atelier fixture Account: {string.Join("; ", result.Errors.Select(e => e.Code))}");
        }
    }
}
