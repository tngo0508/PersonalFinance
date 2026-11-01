using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Fund page: one Tracked Fund's header (name, role, net assets, fee, 1/3/5-year annualized return), then
/// growth, risk, similar funds, what it holds and where the money goes. It reads the funds, fund returns, macro, fund
/// facts, fund overlap and fund exposure datasets; each that has never loaded or could not be read leaves only the
/// sections that need it unavailable. The holdings come from the funds data, which the page always has, since the
/// fund is found in it.
/// </summary>
public sealed class ResearchFundPageViewModel
{
    /// <summary>The windows the header gives an annualized return for, in years.</summary>
    public static readonly IReadOnlyList<int> Windows = [1, 3, 5];

    /// <summary>How many holdings "What it holds" lists.</summary>
    public const int TopHoldingCount = 10;

    /// <summary>How many issuer countries "Where the money goes" lists.</summary>
    public const int TopCountryCount = 10;

    /// <summary>
    /// Builds the page for <paramref name="fund"/>, a Tracked Fund of <paramref name="funds"/>; every other dataset is
    /// null when it could not be read. A well-formed <paramref name="basket"/> is kept on every Retry.
    /// </summary>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    /// <param name="fund">The fund the page is about.</param>
    /// <param name="funds">The funds snapshot (the Tracked Funds and their quarterly holdings).</param>
    /// <param name="returns">The fund returns snapshot, or null.</param>
    /// <param name="macro">The macro snapshot (inflation and the Treasury bill rate), or null.</param>
    /// <param name="facts">The fund facts snapshot (expense ratios and net assets), or null.</param>
    /// <param name="overlap">The fund overlap snapshot, or null.</param>
    /// <param name="exposure">The fund exposure snapshot (issuer countries), or null.</param>
    /// <param name="basket">The address's <c>basket</c>, e.g. <c>SPY:60,QQQ:40</c>.</param>
    public ResearchFundPageViewModel(IStringLocalizer localizer, TrackedFundDto fund, FundsSnapshotDto funds,
        FundReturnsSnapshotDto? returns, MacroSnapshotDto? macro, FundFactsSnapshotDto? facts,
        FundOverlapSnapshotDto? overlap, FundExposureSnapshotDto? exposure, string? basket)
    {
        Fund = fund;
        Funds = funds;
        FundReturns = returns;
        Macro = macro;
        FundFacts = facts;
        FundOverlap = overlap;
        Exposure = exposure?.Funds.FirstOrDefault(f => string.Equals(f.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase));
        FundExposure = Exposure is null ? null : exposure;

        string pageTitle = localizer["Research.Title"];
        Dictionary<string, string> retryRoute = new(StringComparer.Ordinal) { ["ticker"] = fund.Ticker };
        string? trimmedBasket = basket?.Trim();
        if (ResearchAddress.IsWellFormedBasket(trimmedBasket))
        {
            retryRoute["basket"] = trimmedBasket;
        }
        ReturnsUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundReturns"], retryRoute);
        OverlapUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundOverlap"], retryRoute);
        ExposureUnavailableState = Unavailable(pageTitle, localizer["Research.Unavailable.FundExposure"], retryRoute);

        // Later quarters overwrite earlier ones: the fund keeps its latest report.
        (string Period, FundHoldingsDto Report)? latest = null;
        foreach (FundsPeriodDto period in funds.Values)
        {
            FundHoldingsDto? report = period.Funds.FirstOrDefault(f => string.Equals(f.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase));
            if (report is not null)
            {
                latest = (period.Period, report);
            }
        }
        HoldingsPeriod = latest?.Period;
        LatestReport = latest?.Report;
        NetAssets = LatestReport?.NetAssets is > 0 ? LatestReport.NetAssets : null;
        FundFactsDto? fundFacts = facts?.Funds.FirstOrDefault(f => string.Equals(f.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase));
        ExpenseRatio = fundFacts?.ExpenseRatio;
        AnnualizedReturns = Windows.ToDictionary(years => years, years => returns?.Windows
            .FirstOrDefault(w => w.Years == years)?.Funds
            .FirstOrDefault(f => string.Equals(f.Ticker, fund.Ticker, StringComparison.OrdinalIgnoreCase))?.AnnualizedReturn);
        TopHoldings = (LatestReport?.Categories ?? [])
            .SelectMany(c => c.Top.Select(h => new FundTopHolding(h.Name, h.Percent, c.Name)))
            .OrderByDescending(h => h.Percent)
            .ThenBy(h => h.Name, StringComparer.Ordinal)
            .Take(TopHoldingCount)
            .ToList();
        TopCountries = (Exposure?.Countries ?? [])
            .OrderByDescending(c => c.Percent)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .Take(TopCountryCount)
            .ToList();
    }

    /// <summary>The fund the page is about.</summary>
    public TrackedFundDto Fund { get; }

    /// <summary>The funds snapshot, which every Tracked Fund (for "Compare with" and the risk chart's names) comes from.</summary>
    public FundsSnapshotDto Funds { get; }

    /// <summary>The fund returns: the header's returns, growth, risk and similar funds by correlation; null when not loaded.</summary>
    public FundReturnsSnapshotDto? FundReturns { get; }

    /// <summary>The macro data: inflation for growth, the Treasury bill rate for the basket's Sharpe ratio; null when not loaded.</summary>
    public MacroSnapshotDto? Macro { get; }

    /// <summary>The fund facts: the header's fee and the risk chart's bubble sizes; null when not loaded.</summary>
    public FundFactsSnapshotDto? FundFacts { get; }

    /// <summary>The fund overlap: similar funds by holdings; null when not loaded.</summary>
    public FundOverlapSnapshotDto? FundOverlap { get; }

    /// <summary>The fund exposure snapshot when it holds this fund, for the risk chart's basket; null otherwise.</summary>
    public FundExposureSnapshotDto? FundExposure { get; }

    /// <summary>This fund's issuer countries and currencies; null when the exposure is not loaded or lacks the fund.</summary>
    public FundExposureDto? Exposure { get; }

    /// <summary>The Unavailable State of the sections drawn from the returns, shown when <see cref="FundReturns"/> is null.</summary>
    public UnavailableStateViewModel ReturnsUnavailableState { get; }

    /// <summary>The overlap list's Unavailable State, shown when <see cref="FundOverlap"/> is null.</summary>
    public UnavailableStateViewModel OverlapUnavailableState { get; }

    /// <summary>The countries' Unavailable State, shown when <see cref="Exposure"/> is null.</summary>
    public UnavailableStateViewModel ExposureUnavailableState { get; }

    /// <summary>The quarter of the fund's latest report, e.g. <c>2026 Q2</c>; null when it has never reported.</summary>
    public string? HoldingsPeriod { get; }

    /// <summary>The fund's latest report; null when it has never reported.</summary>
    public FundHoldingsDto? LatestReport { get; }

    /// <summary>The fund's net assets in its latest report; null when it has none.</summary>
    public decimal? NetAssets { get; }

    /// <summary>The fund's expense ratio in percent; null when it filed none or the facts did not load ("not filed").</summary>
    public decimal? ExpenseRatio { get; }

    /// <summary>True when the facts loaded, so a missing expense ratio reads "not filed" rather than "not loaded".</summary>
    public bool FactsLoaded => FundFacts is not null;

    /// <summary>The fund's precomputed annualized return for each of <see cref="Windows"/>; null when it has none.</summary>
    public IReadOnlyDictionary<int, decimal?> AnnualizedReturns { get; }

    /// <summary>The fund's largest holdings across its asset categories, largest first; at most <see cref="TopHoldingCount"/>.</summary>
    public IReadOnlyList<FundTopHolding> TopHoldings { get; }

    /// <summary>The fund's largest issuer countries, largest first; at most <see cref="TopCountryCount"/>.</summary>
    public IReadOnlyList<CountryShareDto> TopCountries { get; }

    private static UnavailableStateViewModel Unavailable(string pageTitle, string heading, Dictionary<string, string> route) => new()
    {
        PageTitle = pageTitle,
        Heading = heading,
        RetryController = "Research",
        RetryAction = "Fund",
        RetryRouteValues = new Dictionary<string, string>(route, StringComparer.Ordinal),
    };
}
