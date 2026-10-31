using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Sectors panel (<c>_SectorsPanel</c>): the scrubber, the treemap with its breadcrumb, ranked list and table, the
/// basket view and the side panel. The Sectors page draws it on its own; the tabbed Research page draws it as its
/// Sectors tab. Its element ids are the same on both, so the scripts serve both.
/// </summary>
public sealed class ResearchSectorsPanelViewModel
{
    /// <summary>The Sectors dataset, or its Unavailable State.</summary>
    public required ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto> Sectors { get; init; }

    /// <summary>
    /// Whether the "Market | Your basket" switch and the basket view are drawn: both the Sectors and the funds data
    /// loaded.
    /// </summary>
    public bool BasketView { get; init; }

    /// <summary>True on the tabbed page: the panel is a tab panel, labelled by its tab.</summary>
    public bool InTab { get; init; }

    /// <summary>True when the tabbed page opens on another tab.</summary>
    public bool Hidden { get; init; }

    /// <summary>The group the chart opens zoomed into (<c>Office</c> or <c>Office/Industry</c>), or null.</summary>
    public string? Focus { get; init; }

    /// <summary>
    /// The view the panel opens on, <c>market</c> or <c>basket</c>; null on the tabbed page, which opens on the basket
    /// view whenever a Fund Basket is set.
    /// </summary>
    public string? View { get; init; }

    /// <summary>
    /// The basket view's Unavailable State, when the basket view was asked for but the funds data could not be read;
    /// the market chart still shows.
    /// </summary>
    public UnavailableStateViewModel? BasketUnavailableState { get; init; }
}
