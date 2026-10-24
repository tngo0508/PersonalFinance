using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// A Research refresh builds each dataset on its own: one dataset failing, even at the moment its snapshot is saved,
/// never stops another from being stored. Fakes stand in for SEC and the World Bank, so these tests are about the
/// refresh, not the sources.
/// </summary>
public class ResearchRefreshIsolationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshAsync_WhenSavingOneDatasetFails_StillStoresTheNextDataset()
    {
        var failSectors = new FailingSaveInterceptor(ResearchDatasets.SecFilers, ResearchDatasets.Sectors);
        await using AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Isolation_{Guid.NewGuid():N}")
            .AddInterceptors(failSectors)
            .Options);
        ResearchService service = CreateService(db);

        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.True(failSectors.Thrown);
        Assert.Contains(ResearchDatasets.Sectors, summary.Failed);
        Assert.Contains(ResearchDatasets.World, summary.Built);
        Assert.NotNull((await service.GetWorldAsync()).Snapshot);
        Assert.Null((await service.GetSectorsAsync()).Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_WithTheWorldBankOnly_SkipsTheSecDatasetsWithoutCallingSec()
    {
        await using AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Isolation_{Guid.NewGuid():N}")
            .Options);
        var sec = new FakeSecClient();
        ResearchService service = CreateService(db, sec);

        ResearchRefreshSummary summary = await service.RefreshAsync(ResearchSources.WorldBank);

        Assert.Equal([ResearchDatasets.Sectors, ResearchDatasets.Funds], summary.Skipped);
        Assert.Equal([ResearchDatasets.World], summary.Built);
        Assert.Empty(summary.Failed);
        Assert.Equal(0, sec.Calls);
        Assert.NotNull((await service.GetWorldAsync()).Snapshot);
    }

    private static ResearchService CreateService(AppDbContext db, FakeSecClient? sec = null) =>
        ResearchServiceFactory.Create(db, sec ?? new FakeSecClient(), new FakeWorldBankClient(),
            new ResearchSettings
            {
                SectorsFirstYear = 2025,
                WorldFirstYear = 2025,
                Funds = [new TrackedFund("SPY", "SPDR S&P 500 ETF Trust", 884394)],
            },
            new FixedTimeProvider(Now));

    /// <summary>Throws on a save that adds or changes the snapshot of one of the given datasets.</summary>
    private sealed class FailingSaveInterceptor(params string[] datasets) : SaveChangesInterceptor
    {
        public bool Thrown { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            bool failing = eventData.Context!.ChangeTracker.Entries<ResearchSnapshot>()
                .Any(e => e.State is EntityState.Added or EntityState.Modified && datasets.Contains(e.Entity.Dataset));
            if (failing)
            {
                Thrown = true;
                throw new DbUpdateException("The database refused the snapshot.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>SEC with one filer's Public Float in 2025 and nothing else.</summary>
    private sealed class FakeSecClient : ISecEdgarClient
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<PublicFloatFact>> GetPublicFloatFrameAsync(int year, int quarter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<PublicFloatFact>>(year == 2025 && quarter == 2
                ? [new PublicFloatFact(1, "ONLY CO", new DateOnly(2025, 6, 30), 1_000_000_000m)]
                : []);
        }

        public Task<IReadOnlyList<TotalAssetsFact>> GetTotalAssetsFrameAsync(int year, int quarter,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TotalAssetsFact>>([]);

        public Task<SecFiler?> GetFilerAsync(long cik, CancellationToken cancellationToken) =>
            Task.FromResult<SecFiler?>(null);

        public Task<IReadOnlyList<SecFundSeries>> GetFundSeriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SecFundSeries>>([]);

        public Task<IReadOnlyList<NportFilingRef>> GetNportFilingsAsync(string filer, DateOnly filedFrom,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<NportFilingRef>>([]);
        }

        public Task<NportReport?> GetNportReportAsync(NportFilingRef filing, CancellationToken cancellationToken) =>
            Task.FromResult<NportReport?>(null);
    }

    /// <summary>The World Bank with one country's market cap in 2025.</summary>
    private sealed class FakeWorldBankClient : IWorldBankClient
    {
        public Task<IReadOnlyList<WorldBankCountry>> GetCountriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorldBankCountry>>([new WorldBankCountry("USA", "United States", false)]);

        public Task<WorldBankIndicator> GetIndicatorAsync(string indicator, int firstYear, int lastYear,
            CancellationToken cancellationToken) =>
            Task.FromResult(new WorldBankIndicator(new DateOnly(2026, 10, 8),
                [new WorldBankObservation("USA", 2025, 50_000_000_000_000m)]));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
