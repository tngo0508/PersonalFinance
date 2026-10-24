using System.Globalization;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Turns the World Bank's country list and its market-cap and GDP indicators into the World snapshot: aggregates and
/// codes without an ISO numeric code are dropped, absent values stay absent, and % of GDP is computed only where the
/// same year's GDP exists (and is not zero).
/// </summary>
internal static class WorldSnapshotBuilder
{
    /// <summary>
    /// Builds the snapshot, or null when no country has a market cap in any year.
    /// </summary>
    /// <param name="countries">The World Bank's country list, aggregates included.</param>
    /// <param name="marketCap">The <c>CM.MKT.LCAP.CD</c> indicator (current US$).</param>
    /// <param name="gdp">The <c>NY.GDP.MKTP.CD</c> indicator (current US$).</param>
    /// <param name="fallbackAsOf">The "Data as of" date when the World Bank sent no last-updated date.</param>
    public static WorldSnapshotDto? Build(IReadOnlyList<WorldBankCountry> countries, WorldBankIndicator marketCap,
        WorldBankIndicator gdp, DateOnly fallbackAsOf)
    {
        // The countries that can be drawn: not an aggregate, and with an ISO numeric code for their map shape.
        Dictionary<string, WorldCountryDto> mapped = countries
            .Where(c => !c.IsAggregate)
            .Select(c => (Country: c, Numeric: IsoCountryCodes.NumericOf(c.Iso3)))
            .Where(c => c.Numeric is not null)
            .GroupBy(c => c.Country.Iso3, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => new WorldCountryDto { Id = g.First().Numeric!, Iso3 = g.Key, Name = g.First().Country.Name },
                StringComparer.Ordinal);

        Dictionary<(string Iso3, int Year), decimal> gdpByCountryYear = gdp.Observations
            .Where(o => o.Value is not null && mapped.ContainsKey(o.Iso3))
            .GroupBy(o => (o.Iso3, o.Year))
            .ToDictionary(g => g.Key, g => g.First().Value!.Value);

        List<WorldBankObservation> values = marketCap.Observations
            .Where(o => o.Value is not null && mapped.ContainsKey(o.Iso3))
            .DistinctBy(o => (o.Iso3, o.Year))
            .ToList();
        if (values.Count == 0)
        {
            return null;
        }

        List<WorldCountryDto> listed = values.Select(o => mapped[o.Iso3]).Distinct()
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
        Dictionary<string, int> indexOf = listed.Select((c, i) => (c.Iso3, i))
            .ToDictionary(c => c.Iso3, c => c.i, StringComparer.Ordinal);

        List<WorldPeriodDto> periods = values
            .GroupBy(o => o.Year)
            .OrderBy(g => g.Key)
            .Select(year => new WorldPeriodDto
            {
                Period = year.Key.ToString(CultureInfo.InvariantCulture),
                Countries = year
                    .Select(o => new WorldCountryValueDto
                    {
                        Country = indexOf[o.Iso3],
                        MarketCap = Math.Round(o.Value!.Value, 0),
                        PercentOfGdp = PercentOfGdp(o.Value.Value, gdpByCountryYear, o.Iso3, year.Key),
                    })
                    .OrderByDescending(v => v.MarketCap)
                    .ThenBy(v => v.Country)
                    .ToList(),
            })
            .ToList();

        return new WorldSnapshotDto
        {
            AsOf = marketCap.LastUpdated ?? fallbackAsOf,
            Periods = periods.Select(p => p.Period).ToList(),
            Countries = listed,
            Values = periods,
        };
    }

    private static decimal? PercentOfGdp(decimal marketCap, Dictionary<(string Iso3, int Year), decimal> gdp,
        string iso3, int year) =>
        gdp.TryGetValue((iso3, year), out decimal value) && value > 0
            ? Math.Round(marketCap / value * 100m, 1)
            : null;
}
