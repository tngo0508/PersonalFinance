using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research page: each tab's dataset, or that tab's Unavailable State when the dataset has never loaded or could
/// not be read.
/// </summary>
public sealed class ResearchViewModel
{
    /// <summary>The Sectors snapshot, or null when the tab is unavailable.</summary>
    public SectorsSnapshotDto? Sectors { get; init; }

    /// <summary>True when the Sectors tab shows the Unavailable State.</summary>
    public bool SectorsUnavailable => Sectors is null;

    /// <summary>The Sectors tab's Unavailable State.</summary>
    public UnavailableStateViewModel SectorsUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the Sectors data",
        RetryController = "Research",
    };

    /// <summary>The date the Sectors data is as of ("Data as of").</summary>
    public DateOnly? SectorsAsOf => Sectors?.AsOf;

    /// <summary>The latest year of the Sectors data, which the page opens on.</summary>
    public SectorsPeriodDto? SectorsLatestPeriod => Sectors?.Values.LastOrDefault();

    /// <summary>The ETFs snapshot, or null when the tab is unavailable.</summary>
    public EtfsSnapshotDto? Etfs { get; init; }

    /// <summary>True when the ETFs tab shows the Unavailable State.</summary>
    public bool EtfsUnavailable => Etfs is null || Etfs.Values.Count == 0;

    /// <summary>The ETFs tab's Unavailable State.</summary>
    public UnavailableStateViewModel EtfsUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the ETFs data",
        RetryController = "Research",
    };

    /// <summary>The latest report date of the ETFs data ("Data as of").</summary>
    public DateOnly? EtfsAsOf => Etfs?.AsOf;

    /// <summary>The latest quarter of the ETFs data, which the tab opens on.</summary>
    public EtfsPeriodDto? EtfsLatestPeriod => Etfs?.Values.LastOrDefault();
    /// <summary>The World snapshot, or null when the tab is unavailable.</summary>
    public WorldSnapshotDto? World { get; init; }

    /// <summary>True when the World tab shows the Unavailable State (never loaded, unreadable, or no year).</summary>
    public bool WorldUnavailable => World is null || World.Values.Count == 0;

    /// <summary>The World tab's Unavailable State.</summary>
    public UnavailableStateViewModel WorldUnavailableState { get; } = new()
    {
        PageTitle = "Research",
        Heading = "We couldn't load the World data",
        RetryController = "Research",
    };

    /// <summary>The date the World data is as of ("Data as of").</summary>
    public DateOnly? WorldAsOf => World?.AsOf;
}
