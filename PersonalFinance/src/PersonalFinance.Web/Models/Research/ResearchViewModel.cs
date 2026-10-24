using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;

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

    /// <summary>The World tab's id.</summary>
    public const string WorldTab = "world";

    /// <summary>The Companies tab's id.</summary>
    public const string CompaniesTab = "companies";

    private static readonly string[] Tabs = [SectorsTab, FundsTab, WorldTab, CompaniesTab];

    /// <summary>
    /// The page over each dataset; a null dataset makes its tab unavailable. It opens on <paramref name="tab"/> when
    /// that names a tab (the Funds tab's first id, <see cref="LegacyFundsTab"/>, included), and on the Companies tab
    /// looks up <paramref name="ticker"/> when it is a valid ticker.
    /// </summary>
    public ResearchViewModel(SectorsSnapshotDto? sectors, FundsSnapshotDto? funds, WorldSnapshotDto? world,
        string? tab = null, string? ticker = null)
    {
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

    /// <summary>The World tab, which opens on the latest year.</summary>
    public ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; }
}
