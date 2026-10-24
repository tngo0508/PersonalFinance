using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research page: each tab's dataset, or that tab's Unavailable State when the dataset has never loaded or could
/// not be read.
/// </summary>
public sealed class ResearchViewModel
{
    /// <summary>The page over each dataset; a null dataset makes its tab unavailable.</summary>
    public ResearchViewModel(SectorsSnapshotDto? sectors, EtfsSnapshotDto? etfs, WorldSnapshotDto? world)
    {
        Sectors = new(sectors, "We couldn't load the Sectors data");
        Etfs = new(etfs, "We couldn't load the ETFs data");
        World = new(world, "We couldn't load the World data");
    }

    /// <summary>The Sectors tab, which the page opens on at the latest year.</summary>
    public ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto> Sectors { get; }

    /// <summary>The ETFs tab, which opens on the latest quarter.</summary>
    public ResearchTabViewModel<EtfsSnapshotDto, EtfsPeriodDto> Etfs { get; }

    /// <summary>The World tab, which opens on the latest year.</summary>
    public ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; }
}
