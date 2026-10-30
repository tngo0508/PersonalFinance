using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Features.Learning;

/// <summary>
/// Stores a Lesson 1 completion from two days ago for one fixed learner at startup, for the Atelier E2E smoke: Lesson 2,
/// and with it the Review (#520), opens only after a local midnight, which a test run cannot wait for. The Web app's
/// <c>LearningFixtureAccountSeeder</c> creates the Account with the same <see cref="UserId"/>. On only when the host is
/// Development and <c>E2E_SEED_LEARNING=true</c>, like <c>E2E_SEED_COMMUNITY</c>; never in Production. A learner who
/// already finished Lesson 1 is left alone.
/// </summary>
internal static class LearningFixtureSeeder
{
    /// <summary>The setting (an environment variable in CI) that turns the fixture on.</summary>
    public const string Setting = "E2E_SEED_LEARNING";

    /// <summary>The fixture learner's user id; the Web app's fixture Account has the same Identity id.</summary>
    public const string UserId = "e2e-atelier-learner";

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

        await app.MigrateAndSeedDatabaseAsync(cancellationToken);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        await SeedAsync(services.GetRequiredService<AppDbContext>(), services.GetRequiredService<ILearningContent>(),
            services.GetRequiredService<TimeProvider>(), cancellationToken);
        app.Logger.LogWarning("{Setting} is on: stored the Atelier fixture learner for E2E tests.", Setting);
    }

    /// <summary>
    /// Stores the first Lesson as finished two days ago in UTC, so the second is open in every time zone, unless the
    /// learner already finished it or there are no Lessons.
    /// </summary>
    internal static async Task SeedAsync(AppDbContext db, ILearningContent content, TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        string? firstLesson = content.GetLessons("en").OrderBy(l => l.Day).Select(l => l.Slug).FirstOrDefault();
        if (firstLesson is null)
        {
            return;
        }

        bool finished = await db.LessonCompletions.AsNoTracking()
            .AnyAsync(c => c.UserId == UserId && c.LessonSlug == firstLesson, cancellationToken);
        if (finished)
        {
            return;
        }

        DateTime twoDaysAgo = timeProvider.GetUtcNow().UtcDateTime.AddDays(-2);
        db.LessonCompletions.Add(new LessonCompletion
        {
            UserId = UserId,
            LessonSlug = firstLesson,
            FinishedAt = twoDaysAgo,
            TimeZoneId = "UTC",
            CreatedAt = twoDaysAgo,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
