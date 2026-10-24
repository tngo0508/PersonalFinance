using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds the fund overlap snapshot from each fund's latest N-PORT report. Securities are matched across funds by
/// their key (ISIN, else CUSIP), in memory only: the snapshot holds overlap percentages and holding names, never an
/// identifier (ADR 0010). Overlap is by weight, the sum over shared securities of the smaller weight, so two funds
/// that hold the same companies in very different amounts overlap little.
/// </summary>
internal static class FundOverlapBuilder
{
    /// <summary>How many of its largest shared holdings each pair lists.</summary>
    public const int SharedPerPair = 25;

    /// <summary>
    /// The overlap of every pair of the given funds, in their order. A holding counts when it has a security key, a
    /// positive weight and is not securities-lending cash collateral; one security held in several lines is summed.
    /// </summary>
    public static FundOverlapSnapshotDto Build(IReadOnlyList<LatestFundReport> reports)
    {
        List<Dictionary<string, (decimal Weight, string Name)>> weights = reports.Select(WeightsOf).ToList();
        // Each shared security gets one index, by its key (in memory only), so two securities with one issuer name
        // (e.g. two share classes) stay apart; the name list may then repeat a name.
        var names = new List<string>();
        var keyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var pairs = new List<FundOverlapPairDto>();

        for (int a = 0; a < reports.Count; a++)
        {
            for (int b = a + 1; b < reports.Count; b++)
            {
                List<(string Key, decimal A, decimal B)> shared = weights[a]
                    .Where(entry => weights[b].ContainsKey(entry.Key))
                    .Select(entry => (entry.Key, entry.Value.Weight, weights[b][entry.Key].Weight))
                    .OrderByDescending(s => Math.Min(s.Item2, s.Item3))
                    .ThenBy(s => weights[a][s.Key].Name, StringComparer.Ordinal)
                    .ToList();
                pairs.Add(new FundOverlapPairDto
                {
                    A = a,
                    B = b,
                    Overlap = FundsSnapshotBuilder.Round(shared.Sum(s => Math.Min(s.A, s.B))),
                    CoveredA = FundsSnapshotBuilder.Round(shared.Sum(s => s.A)),
                    CoveredB = FundsSnapshotBuilder.Round(shared.Sum(s => s.B)),
                    SharedCount = shared.Count,
                    Shared = shared.Take(SharedPerPair).Select(s => new FundOverlapHoldingDto
                    {
                        Holding = IndexOf(s.Key, weights[a][s.Key].Name, names, keyIndex),
                        WeightA = FundsSnapshotBuilder.Round(s.A),
                        WeightB = FundsSnapshotBuilder.Round(s.B),
                    }).ToList(),
                });
            }
        }

        return new FundOverlapSnapshotDto
        {
            AsOf = reports.Count == 0 ? default : reports.Max(r => r.Report.ReportDate),
            Funds = reports.Select(r => new FundOverlapFundDto
            {
                Ticker = r.Ticker,
                ReportDate = r.Report.ReportDate,
                Accession = r.Accession,
            }).ToList(),
            Holdings = names,
            Pairs = pairs,
        };
    }

    /// <summary>
    /// A fund's weight per security key, with the name to show for it (the first line's, tidied). Holdings without a
    /// key, with no positive weight, or that are cash collateral are left out.
    /// </summary>
    private static Dictionary<string, (decimal Weight, string Name)> WeightsOf(LatestFundReport report)
    {
        var result = new Dictionary<string, (decimal Weight, string Name)>(StringComparer.Ordinal);
        foreach (NportHolding holding in report.Report.Holdings)
        {
            if (holding.SecurityKey is not { } key || holding.Percent <= 0m || holding.IsCashCollateral)
            {
                continue;
            }

            result[key] = result.TryGetValue(key, out (decimal Weight, string Name) existing)
                ? (existing.Weight + holding.Percent, existing.Name)
                : (holding.Percent, FundsSnapshotBuilder.HoldingName(holding.Name));
        }

        return result;
    }

    /// <summary>The index of a security in the shared name list, by its key, adding its name on first use.</summary>
    private static int IndexOf(string key, string name, List<string> names, Dictionary<string, int> index)
    {
        if (!index.TryGetValue(key, out int position))
        {
            position = names.Count;
            names.Add(name);
            index[key] = position;
        }

        return position;
    }
}
