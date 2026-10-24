using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// Rebuilds the fund industries dataset from each Tracked Fund's latest Form N-PORT report. Each stock's issuer is
/// matched to an SEC filer by name (<see cref="IssuerCikIndex"/>, over SEC's company list and
/// <see cref="ResearchSettings.IssuerCikOverrides"/>), and placed by that filer's SEC submissions: those the Sectors
/// refresh has read, then those this refresh has read before, then new reads, largest holdings first and at most
/// <see cref="ResearchSettings.FundIndustrySubmissionsPerRefresh"/> a run. It reads reports only when some fund has
/// filed a newer one, the override list or the matching has changed, or a filer was left unread last time.
/// </summary>
internal sealed class FundIndustriesRefresher : IResearchDatasetRefresher
{
    private const string DatasetName = "industries";

    // Raised when the matching or the build changes, so the next refresh rebuilds from the same reports.
    private const string BuildVersion = "2";

    // Ends the signature of a snapshot built while some matched filers were still unread.
    private const string UnreadMark = "+unread";

    private readonly ResearchSnapshotStore _store;
    private readonly LatestFundReports _latest;
    private readonly ISecEdgarClient _secClient;
    private readonly ResearchSettings _settings;
    private readonly ILogger<FundIndustriesRefresher> _logger;

    public FundIndustriesRefresher(ResearchSnapshotStore store, LatestFundReports latest, ISecEdgarClient secClient,
        ResearchSettings settings, ILogger<FundIndustriesRefresher> logger)
    {
        _store = store;
        _latest = latest;
        _secClient = secClient;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Dataset => ResearchDatasets.FundIndustries;

    /// <inheritdoc />
    public ResearchSources Source => ResearchSources.SecEdgar;

    /// <inheritdoc />
    public bool IsConfigured => _settings.Funds.Count > 0;

    /// <summary>
    /// Keeps the stored snapshot when it is up to date; otherwise reads each fund's latest report, matches and places
    /// its stocks, and stores the snapshot. A fund whose filings cannot be listed or read is left out.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        string signature = SignatureOf(_settings.IssuerCikOverrides);
        IReadOnlyList<ListedFund> listed = await _latest.ListAsync(DatasetName, cancellationToken);
        FundIndustriesSnapshotDto? stored = await _store.ReadAsync<FundIndustriesSnapshotDto>(
            ResearchDatasets.FundIndustries, cancellationToken);
        if (stored is not null && stored.Signature == signature
            && LatestFundReports.UpToDate(listed, stored.Funds.Select(f => (f.Ticker, f.Accession)).ToList()))
        {
            return;
        }

        // Read first, so a failure here downloads no report.
        IReadOnlyList<SecCompanyListing> listings = await _secClient.GetCompanyTickersAsync(cancellationToken);
        var reports = new List<LatestFundReport>();
        foreach (ListedFund fund in listed)
        {
            if (await _latest.ReadAsync(fund, DatasetName, cancellationToken) is { } report)
            {
                reports.Add(report);
            }
        }

        if (reports.Count == 0)
        {
            throw new InvalidOperationException("No fund has a readable N-PORT report for the industries.");
        }

        var index = new IssuerCikIndex(listings, _settings.IssuerCikOverrides);
        // Each holding is looked up once; the builder reads the same answers.
        var matches = new Dictionary<NportHolding, long?>(ReferenceEqualityComparer.Instance);
        foreach (NportHolding holding in reports.SelectMany(r => r.Report.Holdings).Where(h => !h.IsCashCollateral))
        {
            matches[holding] = index.Find(holding.Name, holding.Country);
        }

        DateOnly asOf = reports.Max(r => r.Report.ReportDate);
        string quarter = string.Create(CultureInfo.InvariantCulture, $"{asOf.Year}Q{(asOf.Month - 1) / 3 + 1}");
        (Dictionary<long, SecFiler> filers, bool allRead) = await ReadFilersAsync(matches, quarter, asOf, cancellationToken);
        FundIndustriesSnapshotDto snapshot = FundIndustriesBuilder.Build(reports,
            h => matches.GetValueOrDefault(h), filers, allRead ? signature : signature + UnreadMark);
        await _store.StoreAsync(ResearchDatasets.FundIndustries, quarter, quarter, asOf, snapshot, cancellationToken);
    }

    /// <summary>
    /// What the snapshot is built with: the build version and the override list, as a short hash.
    /// </summary>
    internal static string SignatureOf(IReadOnlyList<IssuerCikOverride> overrides)
    {
        string text = string.Join('\n', overrides
            .Select(o => string.Create(CultureInfo.InvariantCulture, $"{o.Name}={o.Cik}"))
            .Order(StringComparer.Ordinal)
            .Prepend(BuildVersion));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>
    /// The submissions of every matched filer that can be had this run, and whether that is all of them. Filers read
    /// by the Sectors refresh or by an earlier run of this one are not read again; new reads go largest holding first,
    /// up to the per-run limit, and are kept for next time (a filer SEC has none for is kept with no fields).
    /// </summary>
    private async Task<(Dictionary<long, SecFiler> Filers, bool AllRead)> ReadFilersAsync(
        Dictionary<NportHolding, long?> matches, string quarter, DateOnly asOf, CancellationToken cancellationToken)
    {
        List<SecFiler> own = await _store.ReadAsync<List<SecFiler>>(ResearchDatasets.FundIndustryFilers,
            cancellationToken) ?? [];
        var filers = own.ToDictionary(f => f.Cik);
        foreach (SecFiler filer in await _store.ReadAsync<List<SecFiler>>(ResearchDatasets.SecFilers, cancellationToken) ?? [])
        {
            filers[filer.Cik] = filer; // The Sectors refresh reads its largest filers again each run, so it is newer.
        }

        List<long> unread = matches
            .Where(m => m.Value is not null && !filers.ContainsKey(m.Value.Value))
            .GroupBy(m => m.Value!.Value)
            .OrderByDescending(g => g.Max(m => m.Key.Percent))
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();
        int limit = Math.Max(0, _settings.FundIndustrySubmissionsPerRefresh);
        foreach (long cik in unread.Take(limit))
        {
            SecFiler filer = await _secClient.GetFilerAsync(cik, cancellationToken)
                ?? new SecFiler(cik, string.Empty, null, null, null, null);
            filers[cik] = filer;
            own.Add(filer);
        }

        if (unread.Count > 0)
        {
            await _store.StoreAsync(ResearchDatasets.FundIndustryFilers, quarter, quarter, asOf,
                own.OrderBy(f => f.Cik).ToList(), cancellationToken);
        }

        if (unread.Count > limit && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Read the submissions of {Read} of {Unread} new filers for the fund industries; the rest wait for the next refresh",
                limit, unread.Count);
        }

        return (filers, unread.Count <= limit);
    }
}
