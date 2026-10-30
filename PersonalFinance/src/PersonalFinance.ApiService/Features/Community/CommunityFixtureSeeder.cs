using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// Stores one running daily Challenge at startup, for the Guild Hall E2E smoke: only an Admin writes Challenges, and an
/// E2E run has no Admin (the role is granted to a configured, confirmed account). On only when the host is Development
/// and <c>E2E_SEED_COMMUNITY=true</c>, like <c>E2E_SEED_RESEARCH</c>; never in Production. It is left alone while one is
/// still running, so a run against a SQL Server database adds a new one only after the last has ended.
/// </summary>
internal static class CommunityFixtureSeeder
{
    /// <summary>The setting (an environment variable in CI) that turns the fixture on.</summary>
    public const string Setting = "E2E_SEED_COMMUNITY";

    /// <summary>The fixture Challenge's title, which the E2E smoke looks for.</summary>
    public const string Title = "Pack a lunch";

    /// <summary>True only in Development with <see cref="Setting"/> set to <c>true</c>.</summary>
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        && string.Equals(configuration[Setting], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Migrates, then stores the fixture, when <see cref="IsEnabled"/>.</summary>
    public static async Task SeedIfEnabledAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(app.Environment, app.Configuration))
        {
            return;
        }

        // The schema must exist before the row; migrating again at startup is then a no-op.
        await app.MigrateAndSeedDatabaseAsync(cancellationToken);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
        app.Logger.LogWarning("{Setting} is on: stored the Guild Hall fixture Challenge for E2E tests.", Setting);
    }

    /// <summary>
    /// Stores a daily Challenge that began three days ago and runs four weeks on, so it is running in every time zone,
    /// unless one with <see cref="Title"/> is still running.
    /// </summary>
    internal static async Task SeedAsync(AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        bool running = await db.CommunityChallenges.AsNoTracking()
            .AnyAsync(c => c.Title == Title && c.EndDate > today, cancellationToken);
        if (running)
        {
            return;
        }

        db.CommunityChallenges.Add(new CommunityChallenge
        {
            Title = Title,
            Habit = "Bring lunch from home instead of buying it.",
            Period = CommunityChallengePeriod.Daily,
            StartDate = today.AddDays(-3),
            EndDate = today.AddDays(27),
            CreatedAtUtc = now.UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
