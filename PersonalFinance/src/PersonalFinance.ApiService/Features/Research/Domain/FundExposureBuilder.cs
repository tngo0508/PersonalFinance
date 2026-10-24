using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds the fund exposure snapshot: each fund's share of net assets by issuer country and by currency, summed over
/// every holding of its latest report (not only the largest), securities-lending cash collateral left out. The rest of
/// the fund, cash not reported as a holding and holdings with no country or currency, is unallocated.
/// </summary>
internal static class FundExposureBuilder
{
    /// <summary>The exposure of each report, in their order.</summary>
    public static FundExposureSnapshotDto Build(IReadOnlyList<LatestFundReport> reports) => new()
    {
        AsOf = reports.Count == 0 ? default : reports.Max(r => r.Report.ReportDate),
        Funds = reports.Select(ExposureOf).ToList(),
    };

    private static FundExposureDto ExposureOf(LatestFundReport latest)
    {
        List<NportHolding> holdings = latest.Report.Holdings.Where(h => !h.IsCashCollateral).ToList();
        List<CountryShareDto> countries = holdings
            .Where(h => h.Country is not null)
            .GroupBy(h => h.Country!, StringComparer.Ordinal)
            .Select(g => new CountryShareDto
            {
                Code = g.Key,
                MapId = IsoCountryCodes.NumericOfAlpha2(g.Key),
                Region = CountryRegions.Of(g.Key),
                Percent = FundsSnapshotBuilder.Round(g.Sum(h => h.Percent)),
            })
            .Where(c => c.Percent != 0m)
            .OrderByDescending(c => c.Percent)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
        List<CurrencyShareDto> currencies = holdings
            .Where(h => h.Currency is not null)
            .GroupBy(h => h.Currency!, StringComparer.Ordinal)
            .Select(g => new CurrencyShareDto { Code = g.Key, Percent = FundsSnapshotBuilder.Round(g.Sum(h => h.Percent)) })
            .Where(c => c.Percent != 0m)
            .OrderByDescending(c => c.Percent)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
        return new FundExposureDto
        {
            Ticker = latest.Ticker,
            ReportDate = latest.Report.ReportDate,
            Accession = latest.Accession,
            Countries = countries,
            Currencies = currencies,
            // A fund over 100% in its holdings (derivatives, borrowing) has nothing unallocated.
            UnallocatedCountry = Math.Max(0m, FundsSnapshotBuilder.Round(100m - countries.Sum(c => c.Percent))),
            UnallocatedCurrency = Math.Max(0m, FundsSnapshotBuilder.Round(100m - currencies.Sum(c => c.Percent))),
        };
    }
}
