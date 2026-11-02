using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// Builds the <see cref="FundCompareRow"/> list the Funds page's table and the Home page's tracked-funds list share:
/// one row per Tracked Fund with a window's precomputed figures, the fund's expense ratio and its latest net assets.
/// Nothing is computed here beyond picking the window.
/// </summary>
public static class FundCompareRows
{
    /// <summary>
    /// One row per Tracked Fund, in the snapshot's configured order, with the figures of the <paramref name="window"/>
    /// (in years) from <paramref name="returns"/>, the expense ratio from <paramref name="facts"/> and the net assets of
    /// the latest quarter the fund reported in <paramref name="funds"/>. A figure whose dataset is null is null.
    /// </summary>
    public static List<FundCompareRow> Build(FundsSnapshotDto funds, FundReturnsSnapshotDto? returns,
        FundFactsSnapshotDto? facts, int window)
    {
        Dictionary<string, FundWindowMetricsDto> metrics = (returns?.Windows.FirstOrDefault(w => w.Years == window)?.Funds ?? [])
            .GroupBy(f => f.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, decimal?> fees = (facts?.Funds ?? [])
            .GroupBy(f => f.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ExpenseRatio, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, decimal> netAssets = new(StringComparer.OrdinalIgnoreCase);
        foreach (FundsPeriodDto period in funds.Values)
        {
            foreach (FundHoldingsDto report in period.Funds)
            {
                // Later quarters overwrite earlier ones: each fund keeps its latest report.
                netAssets[report.Ticker] = report.NetAssets;
            }
        }
        return funds.Funds.Select(fund =>
        {
            FundWindowMetricsDto? figures = metrics.GetValueOrDefault(fund.Ticker);
            return new FundCompareRow
            {
                Ticker = fund.Ticker,
                Name = fund.Name,
                Role = fund.Role,
                AnnualReturn = figures?.AnnualizedReturn,
                Volatility = figures?.Volatility,
                Sharpe = figures?.Sharpe,
                ExpenseRatio = fees.GetValueOrDefault(fund.Ticker),
                NetAssets = netAssets.GetValueOrDefault(fund.Ticker),
            };
        }).ToList();
    }
}
