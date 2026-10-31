using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Sectors page: the Sectors dataset (the treemap of the largest SEC filers), the funds dataset its basket
/// view needs, the group the chart opens zoomed into and the view it opens on. Each dataset that has never loaded or
/// could not be read shows only its own section's Unavailable State.
/// </summary>
public sealed class ResearchSectorsPageViewModel
{
    /// <summary>The market view: every SEC filer's Public float. The page opens on it unless asked otherwise.</summary>
    public const string MarketView = "market";

    /// <summary>The basket view: the Fund Basket's holdings by SEC industry.</summary>
    public const string BasketView = "basket";

    /// <summary>The most characters one part of <see cref="Focus"/> may have (an SEC office or SIC industry name).</summary>
    public const int MaxFocusPartLength = 80;

    private static readonly System.Text.RegularExpressions.Regex BasketShape =
        new(@"^[A-Za-z0-9.:,\-]{1,200}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Builds the page over <paramref name="sectors"/> and <paramref name="funds"/>, either null when it could not be
    /// read. <paramref name="focus"/> and <paramref name="view"/> come from the address and are checked here: a
    /// malformed focus is dropped, and any view but the basket's is the market.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="sectors">The Sectors snapshot, or null.</param>
    /// <param name="funds">The funds snapshot, or null.</param>
    /// <param name="focus">The address's <c>focus</c>, e.g. <c>Finance/National Commercial Banks</c>.</param>
    /// <param name="view">The address's <c>view</c>: <c>market</c> or <c>basket</c>.</param>
    /// <param name="basket">The address's <c>basket</c> (e.g. <c>SPY:60,QQQ:40</c>), kept on Retry when well-formed.</param>
    public ResearchSectorsPageViewModel(IStringLocalizer localizer, SectorsSnapshotDto? sectors, FundsSnapshotDto? funds,
        string? focus, string? view, string? basket = null)
    {
        Focus = NormalizeFocus(focus);
        View = string.Equals(view?.Trim(), BasketView, StringComparison.OrdinalIgnoreCase) ? BasketView : MarketView;
        string pageTitle = localizer["Research.Title"];
        Dictionary<string, string> retryRoute = new(StringComparer.Ordinal);
        if (Focus is not null)
        {
            retryRoute["focus"] = Focus;
        }
        if (View == BasketView)
        {
            retryRoute["view"] = BasketView;
        }
        string? trimmedBasket = basket?.Trim();
        if (trimmedBasket is not null && BasketShape.IsMatch(trimmedBasket))
        {
            retryRoute["basket"] = trimmedBasket;
        }
        Sectors = new(sectors, pageTitle, localizer["Research.Unavailable.Sectors"], "Sectors", retryRoute);
        Funds = new(funds, pageTitle, localizer["Research.Unavailable.Funds"], "Sectors",
            new Dictionary<string, string>(retryRoute, StringComparer.Ordinal));
    }

    /// <summary>The Sectors dataset, which the chart shows at its latest year.</summary>
    public ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto> Sectors { get; }

    /// <summary>The funds dataset; the basket view needs it, so without it only that view is unavailable.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>
    /// The SEC office, or office and SIC industry, the chart opens zoomed into, as <c>Office</c> or
    /// <c>Office/Industry</c> with each part trimmed; null when none was asked for or it was malformed. The names are
    /// matched in the page, so an unknown one leaves the chart at the top.
    /// </summary>
    public string? Focus { get; }

    /// <summary>The view the page opens on: <see cref="MarketView"/> or <see cref="BasketView"/>.</summary>
    public string View { get; }

    /// <summary>The Sectors panel the page draws, with the page's focus and view.</summary>
    public ResearchSectorsPanelViewModel Panel => new()
    {
        Sectors = Sectors,
        BasketView = !Sectors.Unavailable && !Funds.Unavailable,
        Focus = Focus,
        View = View,
        BasketUnavailableState = View == BasketView && !Sectors.Unavailable && Funds.Unavailable
            ? Funds.UnavailableState
            : null,
    };

    /// <summary>
    /// The focus with each part trimmed, or null unless it has one or two <c>/</c>-separated parts of 1 to
    /// <see cref="MaxFocusPartLength"/> characters each.
    /// </summary>
    private static string? NormalizeFocus(string? focus)
    {
        if (string.IsNullOrWhiteSpace(focus))
        {
            return null;
        }
        string[] parts = focus.Split('/').Select(part => part.Trim()).ToArray();
        return parts.Length <= 2 && parts.All(part => part.Length is > 0 and <= MaxFocusPartLength)
            ? string.Join('/', parts)
            : null;
    }
}
