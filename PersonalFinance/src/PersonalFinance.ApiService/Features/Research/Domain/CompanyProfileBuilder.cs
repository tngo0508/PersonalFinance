using System.Globalization;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds a Company Profile from a company's SEC submissions and XBRL facts. All of the profile's data rules live
/// here:
/// <list type="bullet">
/// <item>Figures come only from annual reports (<see cref="CompanyProfileConcepts.AnnualForms"/>, fiscal period
/// <c>FY</c>), only in the unit of their concept (US dollars, never converted).</item>
/// <item>Each value is placed in the fiscal year whose end it is dated at; the company's own label for that year comes
/// from the annual report whose main period ends on that date.</item>
/// <item>For each fiscal year the most recently filed value wins, so a restatement replaces the original.</item>
/// <item>For each fiscal year the first concept of a figure with a value wins (<see cref="CompanyProfileConcepts"/>).</item>
/// <item>Total liabilities falls back to liabilities-and-equity minus equity; derived figures are null whenever an
/// input is missing or the ratio would be meaningless (a zero or negative base).</item>
/// </list>
/// </summary>
internal static class CompanyProfileBuilder
{
    /// <summary>The most fiscal years a profile shows.</summary>
    public const int MaxFiscalYears = 10;

    /// <summary>The most filings a profile lists.</summary>
    public const int MaxFilings = 20;

    /// <summary>The filings a profile lists: periodic and current reports, with the foreign-issuer equivalents.</summary>
    public static readonly IReadOnlySet<string> ListedForms = new HashSet<string>(StringComparer.Ordinal)
    {
        "10-K", "10-K/A", "10-Q", "10-Q/A", "8-K", "8-K/A", "20-F", "20-F/A", "40-F", "40-F/A", "6-K",
    };

    private const string EdgarUrl = "https://www.sec.gov/";

    // A fiscal year is 52 or 53 weeks, or a calendar year: 364 to 371 days, with some slack for odd year ends.
    private const int MinYearDays = 350;
    private const int MaxYearDays = 380;

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// The Company Profile of the company behind <paramref name="ticker"/>.
    /// </summary>
    /// <param name="ticker">The ticker it was looked up by, as SEC lists it.</param>
    /// <param name="submissions">The company's SEC submissions.</param>
    /// <param name="facts">The company's XBRL facts, or null when SEC has none for it.</param>
    public static CompanyProfileDto Build(string ticker, SecCompanySubmissions submissions, SecCompanyFacts? facts)
    {
        List<CompanyFilingDto> filings = Filings(submissions);
        List<CompanyFiscalYearDto> years = facts is null ? [] : FiscalYears(facts);
        return new CompanyProfileDto
        {
            Cik = submissions.Cik,
            Name = SectorsClassifier.CompanyName(submissions.Name),
            Ticker = ticker,
            Exchange = ExchangeOf(ticker, submissions),
            SicCode = submissions.SicCode,
            SicDescription = submissions.SicDescription is { } sic ? SectorsClassifier.TitleCase(sic) : null,
            IndustryOffice = submissions.OwnerOrg is { } office ? OfficeName(office) : null,
            StateOfIncorporation = submissions.StateOfIncorporation,
            FiscalYearEnd = FiscalYearEndOf(submissions.FiscalYearEnd),
            ReportsUnderIfrs = facts?.HasIfrsFacts ?? false,
            DataAsOf = filings.Count > 0 ? filings.Max(f => f.FiledOn) : null,
            FiscalYears = years,
            Filings = filings,
            AllFilingsUrl = string.Create(CultureInfo.InvariantCulture,
                $"{EdgarUrl}edgar/browse/?CIK={submissions.Cik}"),
        };
    }

    /// <summary>The most recent <see cref="ListedForms"/> filings, newest first, with 8-K items in plain words.</summary>
    private static List<CompanyFilingDto> Filings(SecCompanySubmissions submissions) =>
        submissions.RecentFilings
            .Where(f => ListedForms.Contains(f.Form))
            .OrderByDescending(f => f.FiledOn)
            .ThenByDescending(f => f.AccessionNumber, StringComparer.Ordinal)
            .Take(MaxFilings)
            .Select(f => new CompanyFilingDto
            {
                Form = f.Form,
                FiledOn = f.FiledOn,
                ReportedOn = f.ReportedOn,
                Url = FilingIndexUrl(submissions.Cik, f.AccessionNumber),
                Events = f.Form.StartsWith("8-K", StringComparison.Ordinal)
                    ? f.Items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(EightKItems.Describe).ToList()
                    : [],
            })
            .ToList();

    /// <summary>The filing's index page: <c>Archives/edgar/data/{cik}/{accession without dashes}/{accession}-index.htm</c>.</summary>
    private static string FilingIndexUrl(long cik, string accessionNumber) =>
        EdgarArchive.FileUrl(cik, accessionNumber, $"{accessionNumber}-index.htm");

    /// <summary>The exchange SEC lists for <paramref name="ticker"/>, or for the company's first ticker.</summary>
    private static string? ExchangeOf(string ticker, SecCompanySubmissions submissions)
    {
        int index = submissions.Tickers.ToList().FindIndex(t => string.Equals(t, ticker, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = 0;
        }

        return index < submissions.Exchanges.Count ? submissions.Exchanges[index] : null;
    }

    /// <summary>SEC's <c>06 Technology</c> as <c>Technology</c>.</summary>
    private static string OfficeName(string ownerOrg)
    {
        string[] parts = ownerOrg.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0].All(char.IsAsciiDigit) ? parts[1].Trim() : ownerOrg.Trim();
    }

    /// <summary>SEC's <c>0630</c> as <c>30 June</c>; null when it is not a valid day of the year.</summary>
    private static string? FiscalYearEndOf(string? mmdd) =>
        mmdd is { Length: 4 }
        && int.TryParse(mmdd[..2], NumberStyles.None, CultureInfo.InvariantCulture, out int month)
        && int.TryParse(mmdd[2..], NumberStyles.None, CultureInfo.InvariantCulture, out int day)
        && month is >= 1 and <= 12
        && day >= 1 && day <= DateTime.DaysInMonth(2024, month) // 2024: a leap year, so 29 February is allowed.
            ? new DateOnly(2024, month, day).ToString("d MMMM", English)
            : null;

    /// <summary>The fiscal years with any figure, the latest <see cref="MaxFiscalYears"/>, oldest first.</summary>
    private static List<CompanyFiscalYearDto> FiscalYears(SecCompanyFacts facts)
    {
        Dictionary<CompanyConcept, List<SecFact>> annual = CompanyProfileConcepts.All.ToDictionary(c => c,
            c => facts.Of(c).Where(f => CompanyProfileConcepts.AnnualForms.Contains(f.Form)
                                        && string.Equals(f.FiscalPeriod, "FY", StringComparison.Ordinal)).ToList());
        FiscalCalendar calendar = FiscalCalendar.From(annual);

        // Per figure and fiscal year, the value of the first concept that has one.
        var values = new Dictionary<(CompanyFigure Figure, int Year), decimal>();
        foreach (CompanyConcept concept in CompanyProfileConcepts.All)
        {
            foreach ((int year, decimal value) in LatestPerYear(concept, annual[concept], calendar))
            {
                values.TryAdd((concept.Figure, year), value);
            }
        }

        var built = new SortedDictionary<int, CompanyFiscalYearDto>();
        foreach (int year in values.Keys.Select(k => k.Year).Distinct())
        {
            built[year] = Year(year, calendar, figure => values.TryGetValue((figure, year), out decimal v) ? v : null);
        }

        foreach (CompanyFiscalYearDto year in built.Values)
        {
            CompanyFiscalYearDto? prior = built.GetValueOrDefault(year.FiscalYear - 1);
            year.RevenueGrowth = Growth(year.Revenue, prior?.Revenue);
            year.EpsGrowth = Growth(year.DilutedEps, prior?.DilutedEps);
        }

        return built.Values.TakeLast(MaxFiscalYears).ToList();
    }

    /// <summary>One fiscal year's figures and the ratios derived from them within the year.</summary>
    private static CompanyFiscalYearDto Year(int year, FiscalCalendar calendar, Func<CompanyFigure, decimal?> value)
    {
        decimal? revenue = value(CompanyFigure.Revenue);
        decimal? grossProfit = value(CompanyFigure.GrossProfit);
        decimal? netIncome = value(CompanyFigure.NetIncome);
        decimal? equity = value(CompanyFigure.Equity);
        decimal? liabilities = value(CompanyFigure.TotalLiabilities)
                               ?? value(CompanyFigure.LiabilitiesAndEquity) - equity;
        return new CompanyFiscalYearDto
        {
            FiscalYear = year,
            PeriodEnd = calendar.EndOf(year),
            Revenue = revenue,
            GrossProfit = grossProfit,
            NetIncome = netIncome,
            DilutedEps = value(CompanyFigure.DilutedEps),
            OperatingCashFlow = value(CompanyFigure.OperatingCashFlow),
            TotalAssets = value(CompanyFigure.TotalAssets),
            TotalLiabilities = liabilities,
            Equity = equity,
            SharesOutstanding = value(CompanyFigure.SharesOutstanding),
            PublicFloat = value(CompanyFigure.PublicFloat),
            GrossMargin = Ratio(grossProfit, revenue),
            NetMargin = Ratio(netIncome, revenue),
            LiabilitiesToEquity = Ratio(liabilities, equity),
        };
    }

    /// <summary>
    /// The concept's value per fiscal year: for each year, the value of the most recently filed annual report.
    /// </summary>
    private static IEnumerable<(int Year, decimal Value)> LatestPerYear(CompanyConcept concept, List<SecFact> facts,
        FiscalCalendar calendar)
    {
        var latest = new Dictionary<int, SecFact>();
        foreach (SecFact fact in facts)
        {
            int? year = concept.Timing switch
            {
                CompanyFactTiming.Year when IsFullYear(fact) => calendar.YearEndingOn(fact.End),
                CompanyFactTiming.YearEnd when fact.Start is null && calendar.IsYearEnd(fact.End) =>
                    calendar.YearEndingOn(fact.End),
                CompanyFactTiming.Cover => fact.FiscalYear,
                _ => null,
            };
            if (year is { } y && (!latest.TryGetValue(y, out SecFact? kept) || IsNewer(fact, kept)))
            {
                latest[y] = fact;
            }
        }

        return latest.Select(pair => (pair.Key, pair.Value.Value));
    }

    private static bool IsFullYear(SecFact fact) =>
        fact.Start is { } start && fact.End.DayNumber - start.DayNumber is >= MinYearDays and <= MaxYearDays;

    /// <summary>Filed later, or on the same day under a later accession number.</summary>
    private static bool IsNewer(SecFact fact, SecFact kept) =>
        fact.FiledOn > kept.FiledOn
        || (fact.FiledOn == kept.FiledOn
            && string.CompareOrdinal(fact.AccessionNumber, kept.AccessionNumber) >= 0);

    /// <summary><paramref name="part"/> / <paramref name="whole"/>; null when either is missing or the whole is not positive.</summary>
    private static decimal? Ratio(decimal? part, decimal? whole) =>
        part is { } p && whole is > 0m ? Math.Round(p / whole.Value, 4) : null;

    /// <summary>This year / prior year − 1; null when either is missing or the prior year is not positive.</summary>
    private static decimal? Growth(decimal? current, decimal? prior) =>
        current is { } c && prior is > 0m ? Math.Round(c / prior.Value - 1m, 4) : null;

    /// <summary>
    /// The company's fiscal years: each fiscal year end it reported, and the label it gives that year. The label comes
    /// from the annual report whose main period (its latest full-year or year-end date) ends on that date; a year end
    /// seen only as a prior year in a later report is labelled by the company's usual offset between its labels and
    /// the calendar year its years end in.
    /// </summary>
    private sealed class FiscalCalendar
    {
        private readonly Dictionary<DateOnly, int> _labelByEnd;
        private readonly HashSet<DateOnly> _yearEnds;
        private readonly int _offset;

        private FiscalCalendar(Dictionary<DateOnly, int> labelByEnd, HashSet<DateOnly> yearEnds, int offset)
        {
            _labelByEnd = labelByEnd;
            _yearEnds = yearEnds;
            _offset = offset;
        }

        public static FiscalCalendar From(Dictionary<CompanyConcept, List<SecFact>> annual)
        {
            List<SecFact> dated = annual
                .Where(pair => pair.Key.Timing != CompanyFactTiming.Cover)
                .SelectMany(pair => pair.Key.Timing == CompanyFactTiming.Year
                    ? pair.Value.Where(IsFullYear)
                    : pair.Value.Where(f => f.Start is null))
                .ToList();

            // Each annual report's main period ends on its latest date; its fiscal year labels that year end. When
            // reports disagree (an amendment), the latest filed wins.
            var labelByEnd = new Dictionary<DateOnly, (int Label, DateOnly FiledOn)>();
            foreach (IGrouping<string, SecFact> report in dated.Where(f => f.FiscalYear is not null)
                         .GroupBy(f => f.AccessionNumber, StringComparer.Ordinal))
            {
                SecFact main = report.MaxBy(f => f.End)!;
                if (!labelByEnd.TryGetValue(main.End, out (int Label, DateOnly FiledOn) kept)
                    || main.FiledOn >= kept.FiledOn)
                {
                    labelByEnd[main.End] = (main.FiscalYear!.Value, main.FiledOn);
                }
            }

            int offset = labelByEnd
                .GroupBy(pair => pair.Value.Label - pair.Key.Year)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefault();

            HashSet<DateOnly> yearEnds = [.. labelByEnd.Keys];
            yearEnds.UnionWith(dated.Where(IsFullYear).Select(f => f.End));
            return new FiscalCalendar(labelByEnd.ToDictionary(p => p.Key, p => p.Value.Label), yearEnds, offset);
        }

        /// <summary>True when <paramref name="date"/> is a fiscal year end.</summary>
        public bool IsYearEnd(DateOnly date) => _yearEnds.Contains(date);

        /// <summary>The label of the fiscal year ending on <paramref name="end"/>.</summary>
        public int YearEndingOn(DateOnly end) =>
            _labelByEnd.TryGetValue(end, out int label) ? label : end.Year + _offset;

        /// <summary>The last day of fiscal year <paramref name="year"/>, or null when no figure is dated at it.</summary>
        public DateOnly? EndOf(int year)
        {
            DateOnly? end = null;
            foreach (DateOnly candidate in _yearEnds)
            {
                if (YearEndingOn(candidate) == year && (end is null || candidate > end))
                {
                    end = candidate;
                }
            }

            return end;
        }
    }
}
