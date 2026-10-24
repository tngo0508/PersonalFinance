using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research page: each tab's dataset, or that tab's Unavailable State when the dataset has never loaded or could
/// not be read.
/// </summary>
public sealed class ResearchViewModel
{
    /// <summary>The Sectors tab's id, which the page opens on by default.</summary>
    public const string SectorsTab = "sectors";

    /// <summary>The Funds tab's id.</summary>
    public const string FundsTab = "funds";

    /// <summary>The Funds tab's first id, from when it was the ETFs tab: an old <c>?tab=etfs</c> link still opens it.</summary>
    public const string LegacyFundsTab = "etfs";

    /// <summary>The Performance tab's id.</summary>
    public const string PerformanceTab = "performance";

    /// <summary>The Risk vs return tab's id.</summary>
    public const string RiskTab = "risk";

    /// <summary>The World tab's id.</summary>
    public const string WorldTab = "world";

    /// <summary>The Companies tab's id.</summary>
    public const string CompaniesTab = "companies";

    private static readonly string[] Tabs = [SectorsTab, FundsTab, PerformanceTab, RiskTab, WorldTab, CompaniesTab];

    /// <summary>
    /// The page over each dataset; a null dataset makes its tab unavailable. It opens on <paramref name="tab"/> when
    /// that names a tab (the Funds tab's first id, <see cref="LegacyFundsTab"/>, included), and on the Companies tab
    /// looks up <paramref name="ticker"/> when it is a valid ticker. A null <paramref name="fundOverlap"/> or
    /// <paramref name="fundReturns"/> leaves only that view of the Funds tab unavailable; a null
    /// <paramref name="fundReturns"/> also makes the Performance tab unavailable, and a null <paramref name="macro"/>
    /// leaves the Performance tab without its inflation adjustment. A null <paramref name="fundFacts"/> leaves the Risk vs
    /// return tab without bubble sizes and expense ratios.
    /// </summary>
    public ResearchViewModel(SectorsSnapshotDto? sectors, FundsSnapshotDto? funds, WorldSnapshotDto? world,
        string? tab = null, string? ticker = null, FundOverlapSnapshotDto? fundOverlap = null,
        FundReturnsSnapshotDto? fundReturns = null, MacroSnapshotDto? macro = null, FundFactsSnapshotDto? fundFacts = null)
    {
        FundFacts = fundFacts;
        Macro = macro;
        FundOverlap = fundOverlap;
        FundReturns = fundReturns;
        Sectors = new(sectors, "We couldn't load the Sectors data");
        Funds = new(funds, "We couldn't load the funds data");
        World = new(world, "We couldn't load the World data");
        string? requested = string.Equals(tab?.Trim(), LegacyFundsTab, StringComparison.OrdinalIgnoreCase) ? FundsTab : tab?.Trim();
        SelectedTab = Tabs.FirstOrDefault(t => string.Equals(t, requested, StringComparison.OrdinalIgnoreCase))
                      ?? SectorsTab;
        CompanyTicker = SelectedTab == CompaniesTab && CompanyLookupRules.TryNormalizeTicker(ticker, out string? normalized)
            ? normalized
            : null;
    }

    /// <summary>The id of the tab the page opens on, e.g. <c>sectors</c>.</summary>
    public string SelectedTab { get; }

    /// <summary>The ticker the Companies tab opens on, as SEC lists it, or null for the empty search.</summary>
    public string? CompanyTicker { get; }

    /// <summary>True when the page opens on <paramref name="tab"/>.</summary>
    public bool IsSelected(string tab) => SelectedTab == tab;

    /// <summary>The Sectors tab, which the page opens on at the latest year.</summary>
    public ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto> Sectors { get; }

    /// <summary>The Funds tab, which opens on the latest quarter.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>
    /// The fund overlap of every pair of Tracked Funds, which the Funds tab shows for the Fund Basket's funds; null when
    /// it has never loaded.
    /// </summary>
    public FundOverlapSnapshotDto? FundOverlap { get; }

    /// <summary>The overlap view's Unavailable State, shown on the Funds tab when <see cref="FundOverlap"/> is null.</summary>
    public UnavailableStateViewModel FundOverlapUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the fund overlap data",
        RetryController = "Research",
        RetryRouteValues = new Dictionary<string, string> { ["tab"] = FundsTab },
    };

    /// <summary>
    /// Each Tracked Fund's monthly returns with per-window figures and correlations, which the Funds tab's correlation
    /// view shows; null when it has never loaded.
    /// </summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The correlation view's Unavailable State, shown on the Funds tab when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel FundReturnsUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the fund returns data",
        RetryController = "Research",
        RetryRouteValues = new Dictionary<string, string> { ["tab"] = FundsTab },
    };

    /// <summary>
    /// Monthly CPI-U and interest rates, which the Performance tab uses to adjust returns for inflation; null when it
    /// has never loaded.
    /// </summary>
    public MacroSnapshotDto? Macro { get; }

    /// <summary>The Performance tab's Unavailable State, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel PerformanceUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the fund returns data",
        RetryController = "Research",
        RetryRouteValues = new Dictionary<string, string> { ["tab"] = PerformanceTab },
    };

    /// <summary>
    /// Each Tracked Fund's net assets and expense ratio, which the Risk vs return tab sizes bubbles by and shows in its
    /// card; null when it has never loaded.
    /// </summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>The Risk vs return tab's Unavailable State, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel RiskUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the fund returns data",
        RetryController = "Research",
        RetryRouteValues = new Dictionary<string, string> { ["tab"] = RiskTab },
    };

    /// <summary>The World tab, which opens on the latest year.</summary>
    public ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; }
}
