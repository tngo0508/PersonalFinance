namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Drops the Public Float values SEC holds wrongly before the Sectors snapshot ranks them. Some filers report about
/// 1000 times their real Public Float, some every year; a value that is implausible next to the filer's own total
/// assets or its own other years is left out rather than shown.
/// </summary>
internal static class PublicFloatCleaner
{
    /// <summary>A value more than this many times the filer's total assets nearest that date is dropped.</summary>
    public const decimal MaxTimesTotalAssets = 200m;

    /// <summary>
    /// A value more than this many times the filer's nearest other year is dropped. The nearest other year is the
    /// larger of its nearest earlier and nearest later year, so a real jump in either direction is kept.
    /// </summary>
    public const decimal MaxTimesNearestOtherYear = 30m;

    /// <summary>A value over this (US$ 6 trillion) is dropped: no company's Public Float has come near it.</summary>
    public const decimal MaxPublicFloat = 6_000_000_000_000m;

    /// <summary>
    /// A value over this (US$ 1 trillion) from a filer with no other year in the dataset is dropped: with nothing to
    /// compare it with, only a value a handful of companies have reached is trusted.
    /// </summary>
    public const decimal MaxPublicFloatWithNoOtherYear = 1_000_000_000_000m;

    /// <summary>
    /// Cleans each year's frames: per year, each filer's latest value, less the dropped ones. Returns what is kept, in
    /// the shape <see cref="SectorsSnapshotBuilder.Build"/> takes, and the filer years dropped, by year then CIK.
    /// A filer year on <paramref name="exclusions"/> is dropped whatever its value; a filer year on
    /// <paramref name="keeps"/> is exempt from the <see cref="MaxTimesNearestOtherYear"/> rule only.
    /// </summary>
    public static PublicFloatCleaning Clean(IReadOnlyDictionary<int, IReadOnlyList<PublicFloatFact>> factsByYear,
        IReadOnlyList<TotalAssetsFact> totalAssets, IReadOnlyCollection<FilerYear> exclusions,
        IReadOnlyCollection<FilerYear> keeps)
    {
        var excluded = new HashSet<FilerYear>(exclusions);
        var kept = new HashSet<FilerYear>(keeps);
        Dictionary<long, List<TotalAssetsFact>> assetsByFiler = totalAssets
            .GroupBy(a => a.Cik)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.End).ToList());
        List<(int Year, List<PublicFloatFact> Latest)> years = factsByYear
            .OrderBy(y => y.Key)
            .Select(y => (y.Key, LatestPerFiler(y.Value)))
            .ToList();
        // Per filer, its value in each year it reported one, oldest first. SEC holds a zero for a year a filer
        // reported no Public Float (e.g. the year it merged into a new CIK), which is no year to compare with.
        Dictionary<long, List<(int Year, decimal Value)>> historyByFiler = years
            .SelectMany(y => y.Latest.Where(f => f.Value > 0).Select(f => (f.Cik, y.Year, f.Value)))
            .GroupBy(x => x.Cik)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Year, x.Value)).ToList());

        var keptByYear = new Dictionary<int, IReadOnlyList<PublicFloatFact>>();
        var dropped = new List<FilerYear>();
        foreach ((int year, List<PublicFloatFact> latest) in years)
        {
            var keptThisYear = new List<PublicFloatFact>();
            foreach (PublicFloatFact fact in latest)
            {
                var filerYear = new FilerYear(fact.Cik, year);
                if (excluded.Contains(filerYear)
                    || IsImplausible(fact, year, historyByFiler.GetValueOrDefault(fact.Cik) ?? [], assetsByFiler,
                        kept.Contains(filerYear)))
                {
                    dropped.Add(filerYear);
                }
                else
                {
                    keptThisYear.Add(fact);
                }
            }

            keptByYear[year] = keptThisYear;
        }

        return new PublicFloatCleaning(keptByYear, dropped);
    }

    private static bool IsImplausible(PublicFloatFact fact, int year, List<(int Year, decimal Value)> history,
        Dictionary<long, List<TotalAssetsFact>> assetsByFiler, bool exemptFromOtherYearRule)
    {
        if (fact.Value > MaxPublicFloat)
        {
            return true;
        }

        if (NearestTotalAssets(fact, assetsByFiler) is { } assets && fact.Value > MaxTimesTotalAssets * assets)
        {
            return true;
        }

        return NearestOtherYear(year, history) is { } other
            ? !exemptFromOtherYearRule && fact.Value > MaxTimesNearestOtherYear * other
            : fact.Value > MaxPublicFloatWithNoOtherYear;
    }

    /// <summary>
    /// The larger of the filer's values in its nearest earlier and nearest later year with a value, or null when it
    /// has no other such year.
    /// </summary>
    private static decimal? NearestOtherYear(int year, List<(int Year, decimal Value)> history)
    {
        int later = history.FindIndex(h => h.Year > year);
        int earlier = (later < 0 ? history.Count : later) - 1;
        if (earlier >= 0 && history[earlier].Year == year)
        {
            earlier--;
        }

        return (earlier >= 0, later >= 0) switch
        {
            (true, true) => Math.Max(history[earlier].Value, history[later].Value),
            (true, false) => history[earlier].Value,
            (false, true) => history[later].Value,
            _ => null,
        };
    }

    /// <summary>The filer's total assets dated nearest the value's date (the earlier on a tie), or null.</summary>
    private static decimal? NearestTotalAssets(PublicFloatFact fact,
        Dictionary<long, List<TotalAssetsFact>> assetsByFiler)
    {
        if (!assetsByFiler.TryGetValue(fact.Cik, out List<TotalAssetsFact>? assets))
        {
            return null;
        }

        TotalAssetsFact nearest = assets.MinBy(a => Math.Abs(a.End.DayNumber - fact.End.DayNumber))!;
        return nearest.Value > 0 ? nearest.Value : null;
    }

    /// <summary>Per filer, the latest of the year's values, in CIK order.</summary>
    private static List<PublicFloatFact> LatestPerFiler(IReadOnlyList<PublicFloatFact> facts) =>
        facts.GroupBy(f => f.Cik)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderByDescending(f => f.End).First())
            .ToList();
}
