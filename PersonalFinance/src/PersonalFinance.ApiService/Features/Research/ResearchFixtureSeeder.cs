using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Stores small fixture snapshots of every Research dataset at startup, for the E2E walkthrough: CI and local E2E runs
/// use an InMemory database and cannot reach SEC or the World Bank, so the refresh job never runs there. On only when
/// the host is Development and <c>E2E_SEED_RESEARCH=true</c> (like the Web app's <c>E2E_DISABLE_EMAIL_CONFIRMATION</c>);
/// never in Production. A dataset already stored is left as it is.
/// </summary>
internal static class ResearchFixtureSeeder
{
    /// <summary>The setting (an environment variable in CI) that turns the fixtures on.</summary>
    public const string Setting = "E2E_SEED_RESEARCH";

    /// <summary>True only in Development with <see cref="Setting"/> set to <c>true</c>.</summary>
    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment()
        && string.Equals(configuration[Setting], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Migrates, then stores the fixtures, when <see cref="IsEnabled"/>.</summary>
    public static async Task SeedIfEnabledAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(app.Environment, app.Configuration))
        {
            return;
        }

        // The schema must exist before the rows; migrating again at startup is then a no-op.
        await app.MigrateAndSeedDatabaseAsync(cancellationToken);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
        app.Logger.LogWarning("{Setting} is on: stored the Research fixture snapshots for E2E tests.", Setting);
    }

    /// <summary>Stores each fixture snapshot whose dataset has no row yet.</summary>
    internal static async Task SeedAsync(AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        SectorsSnapshotDto sectors = ResearchFixtureSnapshots.Sectors();
        EtfsSnapshotDto etfs = ResearchFixtureSnapshots.Etfs();
        WorldSnapshotDto world = ResearchFixtureSnapshots.World();
        (string Dataset, List<string> Periods, DateOnly AsOf, object Snapshot)[] fixtures =
        [
            (ResearchService.SectorsDataset, sectors.Periods, sectors.AsOf, sectors),
            (ResearchService.EtfsDataset, etfs.Periods, etfs.AsOf, etfs),
            (ResearchService.WorldDataset, world.Periods, world.AsOf, world),
        ];

        List<string> stored = await db.ResearchSnapshots.AsNoTracking()
            .Select(s => s.Dataset)
            .ToListAsync(cancellationToken);
        foreach ((string dataset, List<string> periods, DateOnly asOf, object snapshot) in fixtures)
        {
            if (stored.Contains(dataset))
            {
                continue;
            }

            db.ResearchSnapshots.Add(new ResearchSnapshot
            {
                Dataset = dataset,
                FirstPeriod = periods[0],
                LastPeriod = periods[^1],
                SourceAsOf = asOf,
                Payload = JsonSerializer.Serialize(snapshot, snapshot.GetType(), JsonSerializerOptions.Web),
                BuiltAt = timeProvider.GetUtcNow().UtcDateTime,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
