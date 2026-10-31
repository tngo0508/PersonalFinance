using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research World page: the World dataset (the country map), the funds and fund exposure datasets its basket view
/// needs, the country the map opens with selected and the view it opens on. The map and the basket view each show only
/// their own Unavailable State when their data has never loaded or could not be read.
/// </summary>
public sealed class ResearchWorldPageViewModel
{
    /// <summary>The market view: stock-market value by country. The page opens on it unless asked otherwise.</summary>
    public const string MarketView = "market";

    /// <summary>The basket view: the Fund Basket's holdings by issuer country.</summary>
    public const string BasketView = "basket";

    /// <summary>
    /// Builds the page over <paramref name="world"/>, <paramref name="funds"/> and <paramref name="exposure"/>, each
    /// null when it could not be read. <paramref name="country"/> and <paramref name="view"/> come from the address
    /// and are checked here: a malformed country is dropped, and any view but the basket's is the market.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="world">The World snapshot, or null.</param>
    /// <param name="funds">The funds snapshot, or null.</param>
    /// <param name="exposure">The fund exposure snapshot, or null.</param>
    /// <param name="country">The address's <c>country</c>, e.g. <c>Japan</c>.</param>
    /// <param name="view">The address's <c>view</c>: <c>market</c> or <c>basket</c>.</param>
    /// <param name="basket">The address's <c>basket</c> (e.g. <c>SPY:60,QQQ:40</c>), kept on Retry when well-formed.</param>
    public ResearchWorldPageViewModel(IStringLocalizer localizer, WorldSnapshotDto? world, FundsSnapshotDto? funds,
        FundExposureSnapshotDto? exposure, string? country, string? view, string? basket = null)
    {
        Country = ResearchAddress.NormalizeCountry(country);
        View = string.Equals(view?.Trim(), BasketView, StringComparison.OrdinalIgnoreCase) ? BasketView : MarketView;
        string pageTitle = localizer["Research.Title"];
        Dictionary<string, string> retryRoute = new(StringComparer.Ordinal);
        if (Country is not null)
        {
            retryRoute["country"] = Country;
        }
        if (View == BasketView)
        {
            retryRoute["view"] = BasketView;
        }
        string? trimmedBasket = basket?.Trim();
        if (ResearchAddress.IsWellFormedBasket(trimmedBasket))
        {
            retryRoute["basket"] = trimmedBasket;
        }
        World = new(world, pageTitle, localizer["Research.Unavailable.World"], "World", retryRoute);
        Funds = new(funds, pageTitle, localizer["Research.Unavailable.Funds"], "World",
            new Dictionary<string, string>(retryRoute, StringComparer.Ordinal));
        Exposure = exposure is { Funds.Count: > 0 } ? exposure : null;
        ExposureUnavailableState = new UnavailableStateViewModel
        {
            PageTitle = pageTitle,
            Heading = localizer["Research.Unavailable.FundExposure"],
            RetryController = "Research",
            RetryAction = "World",
            RetryRouteValues = new Dictionary<string, string>(retryRoute, StringComparer.Ordinal),
        };
    }

    /// <summary>The World dataset, which the map shows at its latest year.</summary>
    public ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto> World { get; }

    /// <summary>The funds dataset; the basket view needs it, so without it only that view is unavailable.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>
    /// Each Tracked Fund's share by issuer country and currency, which the basket view weights; null when it could not
    /// be read or holds no fund, so only the basket view is unavailable.
    /// </summary>
    public FundExposureSnapshotDto? Exposure { get; }

    /// <summary>The basket view's Unavailable State when <see cref="Exposure"/> is null.</summary>
    public UnavailableStateViewModel ExposureUnavailableState { get; }

    /// <summary>
    /// The country the map opens with selected, trimmed; null when none was asked for or it was malformed. The name is
    /// matched in the page, so an unknown one selects nothing.
    /// </summary>
    public string? Country { get; }

    /// <summary>The view the page opens on: <see cref="MarketView"/> or <see cref="BasketView"/>.</summary>
    public string View { get; }

    /// <summary>The World panel the page draws, with the page's country and view.</summary>
    public ResearchWorldPanelViewModel Panel => new()
    {
        World = World,
        Exposure = Exposure,
        BasketView = !Funds.Unavailable && Exposure is not null,
        Country = Country,
        View = View,
        BasketUnavailableState = View != BasketView ? null
            : Funds.Unavailable ? Funds.UnavailableState
            : Exposure is null ? ExposureUnavailableState
            : null,
    };
}
