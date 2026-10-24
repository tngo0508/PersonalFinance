namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads public data from the World Bank's World Development Indicators API (<c>api.worldbank.org/v2</c>, CC BY 4.0).
/// No key is needed.
/// </summary>
internal interface IWorldBankClient
{
    /// <summary>
    /// Reads the World Bank's country list, aggregates (regions, income groups, "World") included and marked as such.
    /// Any failure throws, so a refresh never builds from partial data.
    /// </summary>
    Task<IReadOnlyList<WorldBankCountry>> GetCountriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads one indicator (e.g. <c>CM.MKT.LCAP.CD</c>) for every country and aggregate from
    /// <paramref name="firstYear"/> to <paramref name="lastYear"/>, following every page. An observation the World
    /// Bank has no value for keeps a null value. Fails as <see cref="GetCountriesAsync"/> does, including when the API
    /// answers with an error message.
    /// </summary>
    Task<WorldBankIndicator> GetIndicatorAsync(string indicator, int firstYear, int lastYear,
        CancellationToken cancellationToken);
}

/// <summary>A World Bank country or aggregate: its ISO 3166-1 alpha-3 (or World Bank) code and name.</summary>
internal sealed record WorldBankCountry(string Iso3, string Name, bool IsAggregate);

/// <summary>One indicator's observations, and the date the World Bank last updated it.</summary>
internal sealed record WorldBankIndicator(DateOnly? LastUpdated, IReadOnlyList<WorldBankObservation> Observations);

/// <summary>One country's value of an indicator in one year; null when the World Bank has none.</summary>
internal sealed record WorldBankObservation(string Iso3, int Year, decimal? Value);
