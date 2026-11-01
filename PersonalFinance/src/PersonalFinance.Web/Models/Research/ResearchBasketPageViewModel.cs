using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Basket page: everything about the Fund Basket in one place, an overview then growth, risk, overlap,
/// correlation, sectors and countries. It reads the funds, fund overlap, fund returns, macro, fund facts, fund exposure
/// and Sectors datasets; each that has never loaded or could not be read leaves only the sections that need it
/// unavailable. With an empty basket the page shows one prompt with starting mixes instead of the sections.
/// </summary>
public sealed class ResearchBasketPageViewModel
{
    /// <summary>The overview section's id: the basket's headline figures.</summary>
    public const string OverviewSection = "basket-overview";

    /// <summary>The growth section's id: the Performance chart.</summary>
    public const string GrowthSection = "basket-growth";

    /// <summary>The risk section's id: the risk vs return chart.</summary>
    public const string RiskSection = "basket-risk";

    /// <summary>The overlap section's id.</summary>
    public const string OverlapSection = "basket-overlap";

    /// <summary>The correlation section's id.</summary>
    public const string CorrelationSection = "basket-correlation";

    /// <summary>The sectors section's id: the Sectors chart's basket view.</summary>
    public const string SectorsSection = "basket-sectors";

    /// <summary>The countries section's id: the World basket view.</summary>
    public const string CountriesSection = "basket-countries";

    /// <summary>The sections in page order, with the resource key of each one's title, for the section list.</summary>
    public static readonly IReadOnlyList<(string Id, string TitleKey)> Sections =
    [
        (OverviewSection, "Research.Basket.Section.Overview"),
        (GrowthSection, "Research.Basket.Section.Growth"),
        (RiskSection, "Research.Basket.Section.Risk"),
        (OverlapSection, "Research.Basket.Section.Overlap"),
        (CorrelationSection, "Research.Basket.Section.Correlation"),
        (SectorsSection, "Research.Basket.Section.Sectors"),
        (CountriesSection, "Research.Basket.Section.Countries"),
    ];

    /// <summary>
    /// Builds the page over the seven datasets, each null when it could not be read. <paramref name="basket"/> comes
    /// from the address: it decides whether the page opens on the sections or on the empty prompt, and a well-formed
    /// one is kept on every Retry.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="funds">The funds snapshot (the Tracked Funds), or null.</param>
    /// <param name="overlap">The fund overlap snapshot, or null.</param>
    /// <param name="returns">The fund returns snapshot, or null.</param>
    /// <param name="macro">The macro snapshot (inflation and the Treasury bill rate), or null.</param>
    /// <param name="facts">The fund facts snapshot (expense ratios and net assets), or null.</param>
    /// <param name="exposure">The fund exposure snapshot (issuer countries), or null.</param>
    /// <param name="sectors">The Sectors snapshot, or null.</param>
    /// <param name="basket">The address's <c>basket</c>, e.g. <c>SPY:60,QQQ:40</c>.</param>
    public ResearchBasketPageViewModel(IStringLocalizer localizer, FundsSnapshotDto? funds,
        FundOverlapSnapshotDto? overlap, FundReturnsSnapshotDto? returns, MacroSnapshotDto? macro,
        FundFactsSnapshotDto? facts, FundExposureSnapshotDto? exposure, SectorsSnapshotDto? sectors, string? basket)
    {
        string pageTitle = localizer["Research.Title"];
        Dictionary<string, string> retryRoute = new(StringComparer.Ordinal);
        string? trimmedBasket = basket?.Trim();
        if (ResearchAddress.IsWellFormedBasket(trimmedBasket))
        {
            retryRoute["basket"] = trimmedBasket;
        }
        Funds = new(funds, pageTitle, localizer["Research.Unavailable.Funds"], "Basket", Copy(retryRoute));
        Sectors = new(sectors, pageTitle, localizer["Research.Unavailable.Sectors"], "Basket", Copy(retryRoute));
        FundOverlap = overlap;
        FundReturns = returns;
        Macro = macro;
        FundFacts = facts;
        FundExposure = exposure is { Funds.Count: > 0 } ? exposure : null;
        OverlapUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundOverlap"], retryRoute);
        ReturnsUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundReturns"], retryRoute);
        ExposureUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundExposure"], retryRoute);

        HashSet<string> tracked = new((funds?.Funds ?? []).Select(f => f.Ticker), StringComparer.OrdinalIgnoreCase);
        IsEmpty = !TickersOf(retryRoute.GetValueOrDefault("basket")).Any(tracked.Contains);
        Presets = ResearchBasketPresets.All.Where(p => p.Funds.All(f => tracked.Contains(f.Ticker))).ToList();
    }

    /// <summary>The funds dataset; without it the page shows only its Unavailable State, since the basket is made of its funds.</summary>
    public ResearchTabViewModel<FundsSnapshotDto, FundsPeriodDto> Funds { get; }

    /// <summary>The Sectors dataset, which the sectors section's chart draws the basket in.</summary>
    public ResearchTabViewModel<SectorsSnapshotDto, SectorsPeriodDto> Sectors { get; }

    /// <summary>The fund overlap, which the overlap section needs; null when not loaded.</summary>
    public FundOverlapSnapshotDto? FundOverlap { get; }

    /// <summary>The fund returns, which the overview's return figures, growth, risk and correlation need; null when not loaded.</summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The macro data: inflation for growth, the Treasury bill rate for the Sharpe ratio; null when not loaded.</summary>
    public MacroSnapshotDto? Macro { get; }

    /// <summary>The fund facts: the overview's average fee and the risk chart's bubble sizes; null when not loaded.</summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>The fund exposure: the overview's US share and the countries section; null when not loaded or empty.</summary>
    public FundExposureSnapshotDto? FundExposure { get; }

    /// <summary>The overlap section's Unavailable State, shown when <see cref="FundOverlap"/> is null.</summary>
    public UnavailableStateViewModel OverlapUnavailableState { get; }

    /// <summary>The Unavailable State of the sections drawn from the returns, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel ReturnsUnavailableState { get; }

    /// <summary>The countries section's Unavailable State, shown when <see cref="FundExposure"/> is null.</summary>
    public UnavailableStateViewModel ExposureUnavailableState { get; }

    /// <summary>
    /// True when the address's basket holds no Tracked Fund: the page opens on the prompt and its starting mixes
    /// instead of the sections. The page keeps this in step when the basket changes.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>The starting mixes whose funds are all Tracked Funds, in the order the page offers them.</summary>
    public IReadOnlyList<ResearchBasketPreset> Presets { get; }

    /// <summary>The sectors section: the Sectors panel opening on the basket view, leaving the address alone.</summary>
    public ResearchSectorsPanelViewModel SectorsPanel => new()
    {
        Sectors = Sectors,
        BasketView = !Funds.Unavailable,
        View = ResearchSectorsPageViewModel.BasketView,
        FixedAddress = true,
    };

    /// <summary>The countries section: the World panel's basket view alone, which does not need the World data.</summary>
    public ResearchWorldPanelViewModel WorldPanel => new()
    {
        World = new ResearchTabViewModel<WorldSnapshotDto, WorldPeriodDto>(null, string.Empty, string.Empty),
        Exposure = FundExposure,
        BasketView = !Funds.Unavailable && FundExposure is not null,
        BasketOnly = true,
    };

    /// <summary>The tickers of a well-formed basket value, e.g. <c>SPY:60,QQQ</c> gives SPY and QQQ.</summary>
    private static IEnumerable<string> TickersOf(string? basket) =>
        (basket ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split(':')[0]);

    private static Dictionary<string, string> Copy(Dictionary<string, string> route) => new(route, StringComparer.Ordinal);

    private static UnavailableStateViewModel Unavailable(string pageTitle, string heading, Dictionary<string, string> route) => new()
    {
        PageTitle = pageTitle,
        Heading = heading,
        RetryController = "Research",
        RetryAction = "Basket",
        RetryRouteValues = Copy(route),
    };
}
