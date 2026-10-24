using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the World dataset from the World Bank: its country list and its market-cap and GDP indicators from
/// <see cref="ResearchSettings.WorldFirstYear"/> to last year.
/// </summary>
internal sealed class WorldRefresher : IResearchDatasetRefresher
{
    /// <summary>The World Bank WDI indicator of market capitalisation of listed domestic companies (current US$).</summary>
    public const string MarketCapIndicator = "CM.MKT.LCAP.CD";

    /// <summary>The World Bank WDI indicator of GDP (current US$).</summary>
    public const string GdpIndicator = "NY.GDP.MKTP.CD";

    private readonly ResearchSnapshotStore _store;
    private readonly IWorldBankClient _worldBankClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;

    public WorldRefresher(ResearchSnapshotStore store, IWorldBankClient worldBankClient, ResearchSettings settings,
        TimeProvider timeProvider)
    {
        _store = store;
        _worldBankClient = worldBankClient;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.World;

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int lastYear = now.Year - 1;
        IReadOnlyList<WorldBankCountry> countries = await _worldBankClient.GetCountriesAsync(cancellationToken);
        WorldBankIndicator marketCap = await _worldBankClient.GetIndicatorAsync(MarketCapIndicator,
            _settings.WorldFirstYear, lastYear, cancellationToken);
        WorldBankIndicator gdp = await _worldBankClient.GetIndicatorAsync(GdpIndicator,
            _settings.WorldFirstYear, lastYear, cancellationToken);

        WorldSnapshotDto snapshot = WorldSnapshotBuilder.Build(countries, marketCap, gdp, DateOnly.FromDateTime(now.UtcDateTime))
            ?? throw new InvalidOperationException("The World Bank returned no market cap for any country.");
        await _store.StoreAsync(ResearchDatasets.World, snapshot.Periods[0], snapshot.Periods[^1], snapshot.AsOf,
            snapshot, cancellationToken);
    }
}
