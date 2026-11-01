using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The World panel (<c>_WorldPanel</c>): the "World market | Your basket" switch, the country map with its scrubber,
/// colour scale, country detail and table, and the basket view. The World page draws it on its own; the tabbed Research
/// page draws it as its World tab. Its element ids are the same on both, so the scripts serve both.
/// </summary>
public sealed class ResearchWorldPanelViewModel
{
    /// <summary>The World dataset, or its Unavailable State.</summary>
    public required ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; init; }

    /// <summary>The fund exposure the basket view weights; null when <see cref="BasketView"/> is false.</summary>
    public FundExposureSnapshotDto? Exposure { get; init; }

    /// <summary>
    /// Whether the "World market | Your basket" switch and the basket view are drawn: the funds and the fund exposure
    /// data loaded. It does not need the World data, so the basket view still works when the map is unavailable.
    /// </summary>
    public bool BasketView { get; init; }

    /// <summary>True on the tabbed page: the panel is a tab panel, labelled by its tab, with the Fund Basket bar.</summary>
    public bool InTab { get; init; }

    /// <summary>True when the tabbed page opens on another tab.</summary>
    public bool Hidden { get; init; }

    /// <summary>The country the map opens with selected (matched by name in the page), or null.</summary>
    public string? Country { get; init; }

    /// <summary>
    /// The view the panel opens on, <c>market</c> or <c>basket</c>; null on the tabbed page, which opens on the basket
    /// view whenever a Fund Basket is set.
    /// </summary>
    public string? View { get; init; }

    /// <summary>
    /// The basket view's Unavailable State, when the basket view was asked for but the funds or fund exposure data
    /// could not be read; the map still shows.
    /// </summary>
    public UnavailableStateViewModel? BasketUnavailableState { get; init; }

    /// <summary>
    /// True on the Basket page: only the basket view is drawn, without the switch, the scrubber or the market map, so
    /// the page does not need the World data.
    /// </summary>
    public bool BasketOnly { get; init; }
}
