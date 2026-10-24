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
}
