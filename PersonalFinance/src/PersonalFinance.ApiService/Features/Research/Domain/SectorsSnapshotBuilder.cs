using System.Globalization;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Turns SEC's quarterly Public Float frames into the Sectors snapshot in two steps: <see cref="Rank"/> takes, per
/// year, each filer's latest value across that year's four frames and ranks the largest <see cref="RankedPerPeriod"/>;
/// <see cref="Build"/> then groups them by SEC industry office and SIC industry from the filers' submissions, and sums
/// the rest into "Other" per industry.
/// </summary>
internal static class SectorsSnapshotBuilder
{
    /// <summary>How many filers each period ranks; the rest are summed.</summary>
    public const int RankedPerPeriod = 500;

    /// <summary>
    /// Ranks each year's frames (any order, any quarters), oldest year first. Years with no filer are left out, so
    /// the list is empty when no year has any filer.
    /// </summary>
    public static List<SectorsRankedYear> Rank(IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> factsByYear)
    {
        // Per year, each filer's latest value: a filer reports once a year, as of its own fiscal Q2, which is a
        // different calendar quarter for filers whose fiscal year is not the calendar year.
        List<(int Year, List<PublicFloatFact> Latest)> years = factsByYear
            .OrderBy(y => y.Key)
            .Select(y => (y.Key, LatestPerFiler(y.Value)))
            .Where(y => y.Item2.Count > 0)
            .ToList();

        return years
            .Select(y =>
            {
                List<PublicFloatFact> ordered = y.Latest
                    .OrderByDescending(f => f.Value)
                    .ThenBy(f => f.Cik)
                    .ToList();
                return new SectorsRankedYear(y.Year, ordered.Take(RankedPerPeriod).ToList(),
                    ordered.Skip(RankedPerPeriod).ToList());
            })
            .ToList();
    }

    /// <summary>
    /// Builds the snapshot from the ranked years, placing each filer by its submissions (a filer without any is
    /// Unassigned and Unclassified). Returns null when there is no year, so an empty source never replaces a stored
    /// snapshot.
    /// </summary>
    public static SectorsSnapshotDto? Build(IReadOnlyList<SectorsRankedYear> years,
        IReadOnlyDictionary<long, SecFiler> filers)
    {
        if (years.Count == 0)
        {
            return null;
        }

        var classifier = new SectorsClassifier(filers);
        Dictionary<long, SectorsPlacement> placements = years
            .SelectMany(y => y.Ranked.Concat(y.Others))
            .Select(f => f.Cik)
            .Distinct()
            .ToDictionary(cik => cik, classifier.Place);

        List<SectorsOffice> offices = placements.Values
            .Select(p => p.Office)
            .Distinct()
            .OrderBy(o => o.Order)
            .ThenBy(o => o.Name, StringComparer.Ordinal)
            .ToList();
        Dictionary<SectorsOffice, int> officeIndex = offices
            .Select((o, i) => (Office: o, Index: i))
            .ToDictionary(x => x.Office, x => x.Index);

        // One industry per SIC code and office, in office order and then SIC code order (unclassified last).
        List<SectorsIndustryDto> industries = placements.Values
            .GroupBy(p => (Office: officeIndex[p.Office], p.SicCode))
            .OrderBy(g => g.Key.Office)
            .ThenBy(g => g.Key.SicCode is null)
            .ThenBy(g => g.Key.SicCode, StringComparer.Ordinal)
            .Select(g => new SectorsIndustryDto
            {
                SicCode = g.Key.SicCode,
                Name = g.Select(p => p.Industry).Order(StringComparer.Ordinal).First(),
                Office = g.Key.Office,
                Technology = SectorsClassifier.IsTechnology(offices[g.Key.Office].Name, g.Key.SicCode),
            })
            .ToList();
        Dictionary<(int Office, string? SicCode), int> industryIndex = industries
            .Select((industry, i) => (Key: (industry.Office, industry.SicCode), Index: i))
            .ToDictionary(x => x.Key, x => x.Index);
        int IndustryOf(long cik) => industryIndex[(officeIndex[placements[cik].Office], placements[cik].SicCode)];

        // Every ranked company once, in CIK order, named by its submissions, or else as of the latest year it is
        // ranked in.
        List<SectorsCompanyDto> companies = years
            .SelectMany(p => p.Ranked.Select(f => (p.Year, Fact: f)))
            .GroupBy(x => x.Fact.Cik)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                SecFiler? filer = filers.GetValueOrDefault(g.Key);
                string name = string.IsNullOrWhiteSpace(filer?.Name) ? g.MaxBy(x => x.Year).Fact.EntityName : filer.Name;
                return new SectorsCompanyDto
                {
                    Cik = g.Key,
                    Name = SectorsClassifier.CompanyName(name),
                    Ticker = filer?.Ticker,
                    Industry = IndustryOf(g.Key),
                };
            })
            .ToList();
        Dictionary<long, int> companyIndex = companies
            .Select((c, i) => (c.Cik, Index: i))
            .ToDictionary(x => x.Cik, x => x.Index);

        SectorsRankedYear latest = years[^1];
        return new SectorsSnapshotDto
        {
            AsOf = latest.Ranked.Concat(latest.Others).Max(f => f.End),
            Periods = years.Select(p => p.Year.ToString(CultureInfo.InvariantCulture)).ToList(),
            Offices = offices.Select(o => o.Name).ToList(),
            Industries = industries,
            Companies = companies,
            Values = years.Select(p => new SectorsPeriodDto
            {
                Period = p.Year.ToString(CultureInfo.InvariantCulture),
                Ranked = p.Ranked
                    .Select(f => new SectorsRankedValueDto { Company = companyIndex[f.Cik], PublicFloat = f.Value })
                    .ToList(),
                Other = p.Others
                    .GroupBy(f => IndustryOf(f.Cik))
                    .OrderBy(g => g.Key)
                    .Select(g => new SectorsOtherValueDto
                    {
                        Industry = g.Key,
                        PublicFloat = g.Sum(f => f.Value),
                        Count = g.Count(),
                    })
                    .ToList(),
                RestTotal = p.Others.Sum(f => f.Value),
                RestCount = p.Others.Count,
            }).ToList(),
        };
    }

    private static List<PublicFloatFact> LatestPerFiler(IReadOnlyList<PublicFloatFact> facts) =>
        facts.GroupBy(f => f.Cik)
            .Select(g => g.OrderByDescending(f => f.End).First())
            .ToList();
}
