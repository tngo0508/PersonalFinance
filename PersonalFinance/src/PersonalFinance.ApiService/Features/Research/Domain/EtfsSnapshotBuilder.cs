using System.Globalization;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds the ETFs snapshot: summarises each N-PORT report into asset categories with their largest holdings, then
/// places each fund's reports in calendar quarters.
/// </summary>
internal static class EtfsSnapshotBuilder
{
    /// <summary>How many holdings of each asset category are listed; the rest are summed into "Other".</summary>
    public const int TopHoldings = 25;

    // Percentages are kept to four decimal places (0.0001% of net assets), enough for the smallest holding shown.
    private const int PercentDecimals = 4;

    // N-PORT's asset category codes (Form N-PORT, Item C.4.a), named for display.
    private static readonly Dictionary<string, string> CategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EC"] = "Common equity",
        ["EP"] = "Preferred equity",
        ["DBT"] = "Debt",
        ["STIV"] = "Short-term investment vehicles",
        ["RA"] = "Repurchase agreements",
        ["DE"] = "Equity derivatives",
        ["DFE"] = "Foreign exchange derivatives",
        ["DIR"] = "Interest rate derivatives",
        ["DCR"] = "Credit derivatives",
        ["DCO"] = "Commodity derivatives",
        ["DO"] = "Other derivatives",
        ["ABS-MBS"] = "Mortgage-backed securities",
        ["ABS-ASBS"] = "Asset-backed securities",
        ["ABS-CBDO"] = "Collateralised debt obligations",
        ["ABS-O"] = "Other asset-backed securities",
        ["LON"] = "Loans",
        ["SN"] = "Structured notes",
        ["COMM"] = "Commodities",
        ["RE"] = "Real estate",
        ["OTHER"] = "Other",
    };

    /// <summary>
    /// One report as the snapshot holds it: its holdings grouped by asset category (largest weight first), each
    /// category's <see cref="TopHoldings"/> largest holdings listed and the rest summed, so a category's weight is
    /// exactly its listed holdings plus its "Other".
    /// </summary>
    public static EtfHoldingsDto Summarize(string ticker, NportReport report) => new()
    {
        Ticker = ticker,
        ReportDate = report.ReportDate,
        NetAssets = report.NetAssets,
        HoldingCount = report.Holdings.Count,
        Categories = report.Holdings
            .GroupBy(h => h.AssetCategory.ToUpperInvariant())
            .Select(Category)
            .OrderByDescending(c => c.Percent)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList(),
    };

    private static EtfCategoryDto Category(IGrouping<string, NportHolding> holdings)
    {
        List<(string Name, decimal Percent)> ranked = holdings
            .Select(h => (Name: HoldingName(h.Name), h.Percent))
            .OrderByDescending(h => h.Percent)
            .ThenBy(h => h.Name, StringComparer.Ordinal)
            .ToList();
        List<EtfHoldingDto> top = ranked.Take(TopHoldings)
            .Select(h => new EtfHoldingDto { Name = h.Name, Percent = Round(h.Percent) })
            .ToList();
        decimal otherPercent = Round(ranked.Skip(TopHoldings).Sum(h => h.Percent));
        return new EtfCategoryDto
        {
            Code = holdings.Key,
            Name = CategoryNames.GetValueOrDefault(holdings.Key, holdings.Key),
            Percent = top.Sum(h => h.Percent) + otherPercent,
            HoldingCount = ranked.Count,
            Top = top,
            OtherCount = ranked.Count - top.Count,
            OtherPercent = otherPercent,
        };
    }

    /// <summary>
    /// The snapshot from the filings read so far: for each configured ETF and calendar quarter from
    /// <paramref name="firstQuarter"/>, the report with the latest date in the quarter (of two for the same date, the
    /// later filed). A quarter lists the funds that reported for it, so one fund's missing report leaves the others.
    /// Null when no fund has a report.
    /// </summary>
    public static EtfsSnapshotDto? Build(IReadOnlyList<ResearchEtf> etfs, IEnumerable<EtfFilingRecord> records,
        DateOnly firstQuarter)
    {
        List<string> tickers = etfs.Select(e => e.Ticker).ToList();
        Dictionary<(string Ticker, string Quarter), EtfHoldingsDto> chosen = records
            .Where(r => r.Holdings is not null && r.Holdings.ReportDate >= firstQuarter && tickers.Contains(r.Ticker))
            .GroupBy(r => (r.Ticker, Quarter: QuarterLabel(r.Holdings!.ReportDate)))
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(r => r.Holdings!.ReportDate)
                .ThenByDescending(r => r.FiledOn)
                .ThenByDescending(r => r.AccessionNumber, StringComparer.Ordinal)
                .First().Holdings!);
        if (chosen.Count == 0)
        {
            return null;
        }

        List<string> periods = chosen.Keys.Select(k => k.Quarter).Distinct().Order(StringComparer.Ordinal).ToList();
        return new EtfsSnapshotDto
        {
            AsOf = chosen.Values.Max(h => h.ReportDate),
            Periods = periods,
            Funds = etfs
                .Where(e => chosen.Keys.Any(k => k.Ticker == e.Ticker))
                .Select(e => new EtfFundDto { Ticker = e.Ticker, Name = e.Name })
                .ToList(),
            Values = periods.Select(period => new EtfsPeriodDto
            {
                Period = period,
                Funds = tickers
                    .Where(t => chosen.ContainsKey((t, period)))
                    .Select(t => chosen[(t, period)])
                    .ToList(),
            }).ToList(),
        };
    }

    /// <summary>The calendar quarter of a date, e.g. <c>2026 Q3</c> for 31 July 2026.</summary>
    public static string QuarterLabel(DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date.Year} Q{((date.Month - 1) / 3) + 1}");

    /// <summary>
    /// A holding's name for display: runs of spaces collapsed (futures are often padded, e.g. <c>MSCI EAFE     Sep26</c>)
    /// and title-cased when the report has it all in capitals.
    /// </summary>
    private static string HoldingName(string name) =>
        SectorsClassifier.TitleCase(string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

    private static decimal Round(decimal percent) =>
        Math.Round(percent, PercentDecimals, MidpointRounding.AwayFromZero);
}
