using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Home page: the Tracked Funds with their three-year figures and "Add to basket" buttons, the Fund
/// Basket's starting mixes, the market backdrop, and when each user-facing dataset's source data is as of. It reads
/// the funds, fund returns, fund facts and macro datasets; each that has never loaded or could not be read leaves only
/// the part that needs it unavailable, and the page still renders when the status cannot be read.
/// </summary>
public sealed class ResearchHomeViewModel
{
    /// <summary>The window, in years, of the tracked-funds list's figures.</summary>
    public const int Window = 3;

    /// <summary>
    /// The stored keys of the datasets the freshness list shows, in its order. They mirror
    /// <c>PersonalFinance.ApiService/Features/Research/ResearchDatasets.cs</c>, which Web cannot reference; the
    /// internal datasets the status also lists (the SEC filers, the fund filings, the exchange rates, ...) are left out.
    /// </summary>
    public static readonly string[] DatasetKeys =
        ["sectors", "etfs", "fund-returns", "fund-facts", "fund-overlap", "fund-exposure", "world", "macro"];

    /// <summary>
    /// Builds the page from the datasets' <paramref name="status"/> (or with every dataset not loaded when it is
    /// null) and the four datasets it shows, each null when it could not be read.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="status">The datasets' status, or null when it could not be read.</param>
    /// <param name="funds">The funds snapshot (the Tracked Funds and their quarterly holdings), or null.</param>
    /// <param name="returns">The fund returns snapshot (the per-window figures), or null.</param>
    /// <param name="facts">The fund facts snapshot (the expense ratios), or null.</param>
    /// <param name="macro">The macro snapshot (consumer prices and interest rates), or null.</param>
    public ResearchHomeViewModel(IStringLocalizer localizer, ResearchStatusDto? status, FundsSnapshotDto? funds,
        FundReturnsSnapshotDto? returns, FundFactsSnapshotDto? facts, MacroSnapshotDto? macro)
    {
        Dictionary<string, DateOnly> asOf = (status?.Datasets ?? [])
            .GroupBy(d => d.Dataset, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().SourceAsOf, StringComparer.Ordinal);
        Datasets = DatasetKeys
            .Select(key => new ResearchDatasetFreshness(key, LabelKeyOf(key),
                asOf.TryGetValue(key, out DateOnly date) ? date : null))
            .ToList();

        Funds = new(funds, localizer["Research.Title"], localizer["Research.Unavailable.Funds"]);
        FundReturns = returns;
        FundFacts = facts;
        List<FundCompareRow> rows = Funds.Unavailable ? [] : FundCompareRows.Build(Funds.Snapshot!, returns, facts, Window);
        // The roles in the order the Tracked Funds are configured; the funds without a role come last.
        FundGroups = rows
            .GroupBy(r => r.Role, StringComparer.Ordinal)
            .OrderBy(g => g.Key is null)
            .Select(g => new ResearchHomeFundGroup(g.Key, g.ToList()))
            .ToList();

        HashSet<string>? tracked = funds is null
            ? null
            : funds.Funds.Select(f => f.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);
        PresetsChecked = tracked is not null;
        Presets = tracked is null
            ? ResearchBasketPresets.All
            : ResearchBasketPresets.All.Where(p => p.Funds.All(f => tracked.Contains(f.Ticker))).ToList();

        Backdrop = ResearchMarketBackdrop.From(macro);
    }

    /// <summary>One row per user-facing dataset, in <see cref="DatasetKeys"/> order.</summary>
    public IReadOnlyList<ResearchDatasetFreshness> Datasets { get; }

    /// <summary>How many of <see cref="Datasets"/> have not loaded.</summary>
    public int NotLoadedCount => Datasets.Count(d => d.AsOf is null);

    /// <summary>The funds dataset; without it the tracked-funds list shows its Unavailable State.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>The fund returns, which give the list its return and volatility; null when not loaded.</summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The fund facts, which give the list its fees; null when not loaded.</summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>
    /// The Tracked Funds grouped by role, each group in the configured order of its funds and the groups in the order
    /// their first fund appears, the funds without a role last; empty when the funds data is unavailable.
    /// </summary>
    public IReadOnlyList<ResearchHomeFundGroup> FundGroups { get; }

    /// <summary>
    /// The Fund Basket's starting mixes: only those whose funds are all Tracked Funds when the funds data loaded
    /// (<see cref="PresetsChecked"/>), else every mix for the page's script to check against the search index.
    /// </summary>
    public IReadOnlyList<ResearchBasketPreset> Presets { get; }

    /// <summary>True when <see cref="Presets"/> was already checked against the Tracked Funds here.</summary>
    public bool PresetsChecked { get; }

    /// <summary>The market backdrop from the macro dataset; null when it has not loaded.</summary>
    public ResearchMarketBackdrop? Backdrop { get; }

    /// <summary>The resource key of a dataset's name: <c>fund-returns</c> gives <c>Research.Home.Dataset.FundReturns</c>.</summary>
    private static string LabelKeyOf(string key) =>
        "Research.Home.Dataset." + string.Concat(key.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}

/// <summary>The Tracked Funds of one role on the Home page's list.</summary>
/// <param name="Role">The role, e.g. <c>US broad</c>, or null for the funds without one.</param>
/// <param name="Rows">The funds, in their configured order, with the three-year figures.</param>
public sealed record ResearchHomeFundGroup(string? Role, IReadOnlyList<FundCompareRow> Rows);
