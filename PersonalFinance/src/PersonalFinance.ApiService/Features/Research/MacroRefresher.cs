using System.Globalization;
using System.Xml;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the macro dataset: CPI-U from the Bureau of Labor Statistics, and the effective federal funds rate and the
/// 3-month Treasury bill rate from the Fed Board's H.15 release, monthly from a year before the funds' first quarter.
/// FRED is not used: its terms forbid caching. When the Fed's release cannot be read, the Treasury bill rate comes from
/// the Treasury's daily bill rates instead, and the federal funds rate keeps the months the stored snapshot has.
/// </summary>
internal sealed class MacroRefresher : IResearchDatasetRefresher
{
    private readonly ResearchSnapshotStore _store;
    private readonly IFedRatesClient _fedClient;
    private readonly IUsAgencyClient _agencyClient;
    private readonly ResearchSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MacroRefresher> _logger;

    public MacroRefresher(ResearchSnapshotStore store, IFedRatesClient fedClient, IUsAgencyClient agencyClient,
        ResearchSettings settings, TimeProvider timeProvider, ILogger<MacroRefresher> logger)
    {
        _store = store;
        _fedClient = fedClient;
        _agencyClient = agencyClient;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.Macro;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.UsAgencies;

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <summary>
    /// The first month the dataset holds: a year before the funds' first quarter, so the CPI covers the month before
    /// the first fund return (the base of an inflation-adjusted line).
    /// </summary>
    internal DateOnly FirstMonth => FundReturnsBuilder.MonthOf(_settings.FundsFirstQuarter).AddMonths(-12);

    /// <summary>
    /// Reads the CPI (a failure here fails the dataset, which keeps its stored snapshot), then the rates, and stores
    /// the months from <see cref="FirstMonth"/> to the latest month any series has.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        DateOnly first = FirstMonth;
        IReadOnlyList<(DateOnly Month, decimal Index)> prices =
            await _agencyClient.GetConsumerPricesAsync(first.Year, cancellationToken);
        if (prices.Count == 0)
        {
            throw new InvalidOperationException("BLS returned no CPI-U month.");
        }

        IReadOnlyList<(DateOnly Month, decimal Rate)> fedFunds;
        IReadOnlyList<(DateOnly Month, decimal Rate)> bills;
        string billSource = "H.15";
        try
        {
            FedMonthlyRates rates = await _fedClient.GetMonthlyRatesAsync(cancellationToken);
            fedFunds = rates.FedFunds;
            bills = rates.TreasuryBill;
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or InvalidDataException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not read the Fed's H.15 release; the Treasury bill rate comes from the Treasury and the federal funds rate keeps its stored months");
            fedFunds = await StoredFedFundsAsync(cancellationToken);
            bills = await TreasuryBillsAsync(first.Year, cancellationToken);
            billSource = "Treasury";
        }

        MacroSnapshotDto snapshot = Build(first, prices, fedFunds, bills, billSource);
        await _store.StoreAsync(ResearchDatasets.Macro, MonthLabel(first), MonthLabel(snapshot.Months[^1].Month),
            snapshot.AsOf, snapshot, cancellationToken);
    }

    /// <summary>The months from <paramref name="first"/> to the latest month any series has, each with its values.</summary>
    internal static MacroSnapshotDto Build(DateOnly first, IReadOnlyList<(DateOnly Month, decimal Index)> prices,
        IReadOnlyList<(DateOnly Month, decimal Rate)> fedFunds, IReadOnlyList<(DateOnly Month, decimal Rate)> bills,
        string billSource)
    {
        Dictionary<DateOnly, decimal> cpi = prices.Where(p => p.Month >= first).ToDictionary(p => p.Month, p => p.Index);
        Dictionary<DateOnly, decimal> funds = fedFunds.Where(r => r.Month >= first).GroupBy(r => r.Month)
            .ToDictionary(g => g.Key, g => g.Last().Rate);
        Dictionary<DateOnly, decimal> bill = bills.Where(r => r.Month >= first).GroupBy(r => r.Month)
            .ToDictionary(g => g.Key, g => g.Last().Rate);
        DateOnly last = cpi.Keys.Concat(funds.Keys).Concat(bill.Keys).Max();
        var months = new List<MacroMonthDto>();
        for (DateOnly month = first; month <= last; month = month.AddMonths(1))
        {
            months.Add(new MacroMonthDto
            {
                Month = month,
                ConsumerPrices = cpi.TryGetValue(month, out decimal index) ? index : null,
                FedFunds = funds.TryGetValue(month, out decimal rate) ? rate : null,
                TreasuryBill = bill.TryGetValue(month, out decimal billRate) ? billRate : null,
            });
        }

        return new MacroSnapshotDto
        {
            AsOf = last.AddMonths(1).AddDays(-1),
            Months = months,
            TreasuryBillSource = billSource,
        };
    }

    private static string MonthLabel(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>The federal funds months of the stored snapshot, or none.</summary>
    private async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> StoredFedFundsAsync(CancellationToken cancellationToken)
    {
        MacroSnapshotDto? stored = await _store.ReadAsync<MacroSnapshotDto>(ResearchDatasets.Macro, cancellationToken);
        return stored?.Months
            .Where(m => m.FedFunds is not null)
            .Select(m => (m.Month, m.FedFunds!.Value))
            .ToList() ?? [];
    }

    /// <summary>The Treasury's bill rates, a year at a time to this year; a year that cannot be read is left out.</summary>
    private async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> TreasuryBillsAsync(int firstYear,
        CancellationToken cancellationToken)
    {
        int thisYear = _timeProvider.GetUtcNow().Year;
        var rates = new List<(DateOnly Month, decimal Rate)>();
        for (int year = firstYear; year <= thisYear; year++)
        {
            try
            {
                rates.AddRange(await _agencyClient.GetTreasuryBillRatesAsync(year, cancellationToken));
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException
                                           || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _logger.LogWarning(ex, "Could not read the Treasury's bill rates for {Year}", year);
            }
        }

        return rates;
    }
}
