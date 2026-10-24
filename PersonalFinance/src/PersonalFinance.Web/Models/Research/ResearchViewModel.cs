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

    /// <summary>The latest year's ranked filers, largest first, for the table.</summary>
    public IReadOnlyList<SectorsTableRow> SectorsTableRows =>
        Sectors is null || SectorsLatestPeriod is null
            ? []
            : SectorsLatestPeriod.Ranked
                .Select((value, index) => new SectorsTableRow(index + 1, Sectors.Companies[value.Company].Name,
                    value.PublicFloat))
                .ToList();
}

/// <summary>
/// One row of the Sectors table.
/// </summary>
/// <param name="Rank">1 for the largest filer.</param>
/// <param name="Name">The filer's name as SEC reports it.</param>
/// <param name="PublicFloat">The filer's Public Float (US$).</param>
public sealed record SectorsTableRow(int Rank, string Name, decimal PublicFloat);
