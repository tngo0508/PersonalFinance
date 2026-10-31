using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Funds page: every Tracked Fund compared in a sortable table over a 1, 3 or 5-year window, the risk and
/// correlation views, and the holdings list for one quarter. It reads the funds, fund returns and fund facts datasets;
/// each that has never loaded or could not be read leaves only the views that need it unavailable.
/// </summary>
public sealed class ResearchFundsPageViewModel
{
    /// <summary>The comparison table, which the page opens on unless asked otherwise.</summary>
    public const string TableView = "table";

    /// <summary>The risk vs return chart.</summary>
    public const string RiskView = "risk";

    /// <summary>The correlation matrix.</summary>
    public const string CorrelationView = "correlation";

    /// <summary>The holdings list.</summary>
    public const string HoldingsView = "holdings";

    /// <summary>The window, in years, the table opens on.</summary>
    public const int DefaultWindow = 3;

    /// <summary>Sort by ticker, A to Z.</summary>
    public const string SortTicker = "ticker";

    /// <summary>Sort by role, A to Z.</summary>
    public const string SortRole = "role";

    /// <summary>Sort by annualized return, highest first; the default.</summary>
    public const string SortReturn = "return";

    /// <summary>Sort by volatility, highest first.</summary>
    public const string SortVolatility = "volatility";

    /// <summary>Sort by Sharpe ratio, highest first.</summary>
    public const string SortSharpe = "sharpe";

    /// <summary>Sort by expense ratio, highest first.</summary>
    public const string SortFee = "fee";

    /// <summary>Sort by net assets, largest first.</summary>
    public const string SortAssets = "assets";

    /// <summary>The page's views, in the order the view switch lists them.</summary>
    public static readonly IReadOnlyList<string> Views = [TableView, RiskView, CorrelationView, HoldingsView];

    /// <summary>The windows the table offers, in years.</summary>
    public static readonly IReadOnlyList<int> Windows = [1, 3, 5];

    /// <summary>The table's sort keys, in its column order.</summary>
    public static readonly IReadOnlyList<string> SortKeys =
        [SortTicker, SortRole, SortReturn, SortVolatility, SortSharpe, SortFee, SortAssets];

    /// <summary>
    /// Builds the page over the three datasets, each null when it could not be read. <paramref name="view"/>,
    /// <paramref name="window"/>, <paramref name="sort"/> and <paramref name="quarter"/> come from the address and are
    /// checked here: an unknown value falls back to the table, 3 years, return and the latest quarter.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="funds">The funds snapshot (Tracked Funds and their quarterly holdings), or null.</param>
    /// <param name="returns">The fund returns snapshot (per-window figures and correlations), or null.</param>
    /// <param name="facts">The fund facts snapshot (expense ratios), or null.</param>
    /// <param name="view">The address's <c>view</c>: table, risk, correlation or holdings.</param>
    /// <param name="window">The address's <c>window</c>, in years: 1, 3 or 5.</param>
    /// <param name="sort">The address's <c>sort</c>: one of <see cref="SortKeys"/>.</param>
    /// <param name="quarter">The address's <c>quarter</c>, e.g. <c>2026 Q2</c>: one of the funds snapshot's periods.</param>
    /// <param name="basket">The address's <c>basket</c> (e.g. <c>SPY:60,QQQ:40</c>), kept on Retry when well-formed.</param>
    public ResearchFundsPageViewModel(IStringLocalizer localizer, FundsSnapshotDto? funds,
        FundReturnsSnapshotDto? returns, FundFactsSnapshotDto? facts, string? view, int? window, string? sort,
        string? quarter, string? basket = null)
    {
        View = Views.FirstOrDefault(v => string.Equals(v, view?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? TableView;
        Window = window is { } years && Windows.Contains(years) ? years : DefaultWindow;
        Sort = SortKeys.FirstOrDefault(k => string.Equals(k, sort?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? SortReturn;
        FundReturns = returns;
        FundFacts = facts;
        string pageTitle = localizer["Research.Title"];
        Dictionary<string, string> retryRoute = new(StringComparer.Ordinal)
        {
            ["view"] = View,
            ["window"] = Window.ToString(CultureInfo.InvariantCulture),
            ["sort"] = Sort,
        };
        string? trimmedBasket = basket?.Trim();
        if (ResearchAddress.IsWellFormedBasket(trimmedBasket))
        {
            retryRoute["basket"] = trimmedBasket;
        }
        FundsPeriodDto? latest = funds?.Values.LastOrDefault();
        HoldingsPeriod = funds?.Values.FirstOrDefault(p => string.Equals(p.Period, quarter?.Trim(), StringComparison.OrdinalIgnoreCase))
                         ?? latest;
        if (HoldingsPeriod is not null && !ReferenceEquals(HoldingsPeriod, latest))
        {
            retryRoute["quarter"] = HoldingsPeriod.Period;
        }
        Funds = new(funds, pageTitle, localizer["Research.Unavailable.Funds"], "Funds", retryRoute);
        ReturnsUnavailableState = new UnavailableStateViewModel
        {
            PageTitle = pageTitle,
            Heading = localizer["Research.Unavailable.FundReturns"],
            RetryController = "Research",
            RetryAction = "Funds",
            RetryRouteValues = new Dictionary<string, string>(retryRoute, StringComparer.Ordinal),
        };
        Rows = Funds.Unavailable ? [] : SortRows(BuildRows(Funds.Snapshot!, returns, facts, Window), Sort);
    }

    /// <summary>The funds dataset; without it the table and the holdings list show its Unavailable State.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>The fund returns, which the table's figures, the risk view and the correlation view need; null when not loaded.</summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The fund facts, which give the table its fees and the risk chart its bubble sizes; null when not loaded.</summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>The Unavailable State of the views that need the fund returns, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel ReturnsUnavailableState { get; }

    /// <summary>The view the page opens on: one of <see cref="Views"/>.</summary>
    public string View { get; }

    /// <summary>The table's window, in years: one of <see cref="Windows"/>.</summary>
    public int Window { get; }

    /// <summary>The table's sort key: one of <see cref="SortKeys"/>.</summary>
    public string Sort { get; }

    /// <summary>The quarter the holdings list shows; null when the funds data is unavailable.</summary>
    public FundsPeriodDto? HoldingsPeriod { get; }

    /// <summary>The table's rows, sorted by <see cref="Sort"/> with ties by ticker; empty when the funds data is unavailable.</summary>
    public IReadOnlyList<FundCompareRow> Rows { get; }

    /// <summary>True when the risk view cannot draw: the funds or the fund returns are missing.</summary>
    public bool RiskUnavailable => Funds.Unavailable || FundReturns is null;

    /// <summary>The holdings list at <see cref="HoldingsPeriod"/>, with every quarter in its picker; null without the funds data.</summary>
    public FundHoldingsListViewModel? Holdings => HoldingsPeriod is null
        ? null
        : new FundHoldingsListViewModel { Period = HoldingsPeriod, Funds = Funds.Snapshot!.Funds, Quarters = Funds.Snapshot.Periods };

    /// <summary>
    /// Whether <paramref name="key"/> sorts highest first (the numbers) rather than A to Z (ticker and role).
    /// </summary>
    public static bool SortsDescending(string key) => key is not (SortTicker or SortRole);

    /// <summary>
    /// One row per Tracked Fund, with the window's precomputed figures, the fund's expense ratio and its net assets in
    /// the latest quarter it reported. Nothing is computed here beyond picking the window.
    /// </summary>
    private static List<FundCompareRow> BuildRows(FundsSnapshotDto funds, FundReturnsSnapshotDto? returns,
        FundFactsSnapshotDto? facts, int window)
    {
        Dictionary<string, FundWindowMetricsDto> metrics = (returns?.Windows.FirstOrDefault(w => w.Years == window)?.Funds ?? [])
            .GroupBy(f => f.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, decimal?> fees = (facts?.Funds ?? [])
            .GroupBy(f => f.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ExpenseRatio, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, decimal> netAssets = new(StringComparer.OrdinalIgnoreCase);
        foreach (FundsPeriodDto period in funds.Values)
        {
            foreach (FundHoldingsDto report in period.Funds)
            {
                // Later quarters overwrite earlier ones: each fund keeps its latest report.
                netAssets[report.Ticker] = report.NetAssets;
            }
        }
        return funds.Funds.Select(fund =>
        {
            FundWindowMetricsDto? figures = metrics.GetValueOrDefault(fund.Ticker);
            return new FundCompareRow
            {
                Ticker = fund.Ticker,
                Name = fund.Name,
                Role = fund.Role,
                AnnualReturn = figures?.AnnualizedReturn,
                Volatility = figures?.Volatility,
                Sharpe = figures?.Sharpe,
                ExpenseRatio = fees.GetValueOrDefault(fund.Ticker),
                NetAssets = netAssets.GetValueOrDefault(fund.Ticker),
            };
        }).ToList();
    }

    /// <summary>
    /// The rows by <paramref name="key"/>: numbers highest first and text A to Z, a missing value last, ties by ticker.
    /// </summary>
    private static List<FundCompareRow> SortRows(List<FundCompareRow> rows, string key)
    {
        IOrderedEnumerable<FundCompareRow> ordered = key switch
        {
            SortTicker => rows.OrderBy(r => r.Ticker, StringComparer.Ordinal),
            SortRole => rows.OrderBy(r => r.Role is null).ThenBy(r => r.Role, StringComparer.CurrentCultureIgnoreCase),
            SortVolatility => Descending(rows, r => r.Volatility),
            SortSharpe => Descending(rows, r => r.Sharpe),
            SortFee => Descending(rows, r => r.ExpenseRatio),
            SortAssets => Descending(rows, r => r.NetAssets),
            _ => Descending(rows, r => r.AnnualReturn),
        };
        return ordered.ThenBy(r => r.Ticker, StringComparer.Ordinal).ToList();
    }

    private static IOrderedEnumerable<FundCompareRow> Descending(List<FundCompareRow> rows, Func<FundCompareRow, decimal?> value) =>
        rows.OrderBy(r => value(r) is null).ThenByDescending(value);
}
