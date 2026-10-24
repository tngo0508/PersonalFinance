using System.Globalization;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Turns SEC's quarterly Public Float frames into the Sectors snapshot: per year, each filer's latest value across
/// that year's four frames, the largest <see cref="RankedPerPeriod"/> ranked and the rest summed.
/// </summary>
internal static class SectorsSnapshotBuilder
{
    /// <summary>How many filers each period ranks; the rest are summed.</summary>
    public const int RankedPerPeriod = 500;

    /// <summary>
    /// Builds the snapshot from each year's frames (any order, any quarters). Years with no filer are left out.
    /// Returns null when no year has any filer, so an empty source never replaces a stored snapshot.
    /// </summary>
    public static SectorsSnapshotDto? Build(IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> factsByYear)
    {
        // Per year, each filer's latest value: a filer reports once a year, as of its own fiscal Q2, which is a
        // different calendar quarter for filers whose fiscal year is not the calendar year.
        List<(int Year, List<PublicFloatFact> Latest)> years = factsByYear
            .OrderBy(y => y.Key)
            .Select(y => (y.Key, LatestPerFiler(y.Value)))
            .Where(y => y.Item2.Count > 0)
            .ToList();
        if (years.Count == 0)
        {
            return null;
        }

        List<(int Year, List<PublicFloatFact> Ranked, List<PublicFloatFact> Others)> ranked = years
            .Select(y =>
            {
                List<PublicFloatFact> ordered = y.Latest
                    .OrderByDescending(f => f.Value)
                    .ThenBy(f => f.Cik)
                    .ToList();
                return (y.Year, ordered.Take(RankedPerPeriod).ToList(), ordered.Skip(RankedPerPeriod).ToList());
            })
            .ToList();

        // Every ranked company once, in CIK order, named as of the latest year it is ranked in.
        List<SectorsCompanyDto> companies = ranked
            .SelectMany(p => p.Ranked.Select(f => (p.Year, Fact: f)))
            .GroupBy(x => x.Fact.Cik)
            .OrderBy(g => g.Key)
            .Select(g => new SectorsCompanyDto { Cik = g.Key, Name = g.MaxBy(x => x.Year).Fact.EntityName })
            .ToList();
        Dictionary<long, int> companyIndex = companies
            .Select((c, i) => (c.Cik, Index: i))
            .ToDictionary(x => x.Cik, x => x.Index);

        return new SectorsSnapshotDto
        {
            AsOf = years[^1].Latest.Max(f => f.End),
            Periods = ranked.Select(p => p.Year.ToString(CultureInfo.InvariantCulture)).ToList(),
            Companies = companies,
            Values = ranked.Select(p => new SectorsPeriodDto
            {
                Period = p.Year.ToString(CultureInfo.InvariantCulture),
                Ranked = p.Ranked
                    .Select(f => new SectorsRankedValueDto { Company = companyIndex[f.Cik], PublicFloat = f.Value })
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
