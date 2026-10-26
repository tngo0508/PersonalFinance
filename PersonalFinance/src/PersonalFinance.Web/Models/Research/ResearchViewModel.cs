using Microsoft.Extensions.Localization;
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

    /// <summary>
    /// The page's date format, "31 December 2025" in English: day, month name and year in this order; only the month
    /// name comes from the request culture.
    /// </summary>
    public const string LongDate = "d MMMM yyyy";

    /// <summary>The page's month format, the culture's year-month pattern ("December 2025").</summary>
    public const string MonthYear = "Y";

    private static readonly string[] Tabs = [SectorsTab, FundsTab, PerformanceTab, RiskTab, WorldTab, CompaniesTab];

    /// <summary>
    /// The page over each dataset; a null dataset makes its tab unavailable. It opens on <paramref name="tab"/> when
    /// that names a tab (the Funds tab's first id, <see cref="LegacyFundsTab"/>, included), and on the Companies tab
    /// looks up <paramref name="ticker"/> when it is a valid ticker. A null <paramref name="fundOverlap"/> or
    /// <paramref name="fundReturns"/> leaves only that view of the Funds tab unavailable; a null
    /// <paramref name="fundReturns"/> also makes the Performance tab unavailable, and a null <paramref name="macro"/>
    /// leaves the Performance tab without its inflation adjustment. A null <paramref name="fundFacts"/> leaves the Risk vs
    /// return tab without bubble sizes and expense ratios, and a null <paramref name="fundExposure"/> leaves the World tab
    /// with its market view only. The Unavailable States' text comes from <paramref name="localizer"/>, in the request's
    /// Language.
    /// </summary>
    public ResearchViewModel(IStringLocalizer localizer, SectorsSnapshotDto? sectors, FundsSnapshotDto? funds,
        WorldSnapshotDto? world,
        string? tab = null, string? ticker = null, FundOverlapSnapshotDto? fundOverlap = null,
        FundReturnsSnapshotDto? fundReturns = null, MacroSnapshotDto? macro = null, FundFactsSnapshotDto? fundFacts = null,
        FundExposureSnapshotDto? fundExposure = null)
    {
        FundFacts = fundFacts;
        FundExposure = fundExposure;
        Macro = macro;
        FundOverlap = fundOverlap;
        FundReturns = fundReturns;
        string pageTitle = localizer["Research.Title"];
        Sectors = new(sectors, pageTitle, localizer["Research.Unavailable.Sectors"]);
        Funds = new(funds, pageTitle, localizer["Research.Unavailable.Funds"]);
        World = new(world, pageTitle, localizer["Research.Unavailable.World"]);
        FundOverlapUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundOverlap"], FundsTab);
        string returnsHeading = localizer["Research.Unavailable.FundReturns"];
        FundReturnsUnavailableState = Unavailable(pageTitle, returnsHeading, FundsTab);
        PerformanceUnavailableState = Unavailable(pageTitle, returnsHeading, PerformanceTab);
        RiskUnavailableState = Unavailable(pageTitle, returnsHeading, RiskTab);
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
    public UnavailableStateViewModel FundOverlapUnavailableState { get; }

    /// <summary>
    /// Each Tracked Fund's monthly returns with per-window figures and correlations, which the Funds tab's correlation
    /// view shows; null when it has never loaded.
    /// </summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The correlation view's Unavailable State, shown on the Funds tab when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel FundReturnsUnavailableState { get; }

    /// <summary>
    /// Monthly CPI-U and interest rates, which the Performance tab uses to adjust returns for inflation; null when it
    /// has never loaded.
    /// </summary>
    public MacroSnapshotDto? Macro { get; }

    /// <summary>The Performance tab's Unavailable State, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel PerformanceUnavailableState { get; }

    /// <summary>
    /// Each Tracked Fund's net assets and expense ratio, which the Risk vs return tab sizes bubbles by and shows in its
    /// card; null when it has never loaded.
    /// </summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>The Risk vs return tab's Unavailable State, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel RiskUnavailableState { get; }

    /// <summary>
    /// Each Tracked Fund's share by issuer country and currency, which the World tab's basket view weights; null when
    /// it has never loaded.
    /// </summary>
    public FundExposureSnapshotDto? FundExposure { get; }

    /// <summary>The World tab, which opens on the latest year.</summary>
    public ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; }

    private static UnavailableStateViewModel Unavailable(string pageTitle, string heading, string tab) => new()
    {
        PageTitle = pageTitle,
        Heading = heading,
        RetryController = "Research",
        RetryRouteValues = new Dictionary<string, string> { ["tab"] = tab },
    };
}
