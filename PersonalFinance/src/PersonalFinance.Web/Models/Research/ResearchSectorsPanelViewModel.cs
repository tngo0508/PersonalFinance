using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Sectors panel (<c>_SectorsPanel</c>): the scrubber, the treemap with its breadcrumb, ranked list and table, the
/// basket view and the side panel, on the Sectors page and (basket view only) the Basket page.
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

    /// <summary>The group the chart opens zoomed into (<c>Office</c> or <c>Office/Industry</c>), or null.</summary>
    public string? Focus { get; init; }

    /// <summary>
    /// The view the panel opens on, <c>market</c> or <c>basket</c>; null opens on the market.
    /// </summary>
    public string? View { get; init; }

    /// <summary>
    /// The basket view's Unavailable State, when the basket view was asked for but the funds data could not be read;
    /// the market chart still shows.
    /// </summary>
    public UnavailableStateViewModel? BasketUnavailableState { get; init; }

    /// <summary>
    /// True when the market view is shown but the funds data could not be read: the panel says in a short note that the
    /// basket view is unavailable, where the switch would be, rather than leaving the switch out silently.
    /// </summary>
    public bool BasketViewUnavailableNote { get; init; }

    /// <summary>
    /// True on the Basket page: the "Market | Your basket" switch leaves the address alone, since the address is the
    /// Basket page's.
    /// </summary>
    public bool FixedAddress { get; init; }
}
