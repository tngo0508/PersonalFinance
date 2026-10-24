using PersonalFinance.Shared.DTOs.Research;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Research Feature's public market datasets.
/// </summary>
public interface IResearchApi
{
    /// <summary>
    /// Gets the Sectors snapshot. A dataset that has never loaded answers 503 with a
    /// <see cref="ResearchUnavailableDto"/>, which Refit raises as an <see cref="ApiException"/>.
    /// </summary>
    [Get("/api/Research/sectors")]
    Task<SectorsSnapshotDto> GetSectorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ETFs snapshot. A dataset that has never loaded answers 503 with a <see cref="ResearchUnavailableDto"/>,
    /// which Refit raises as an <see cref="ApiException"/>.
    /// </summary>
    [Get("/api/Research/etfs")]
    Task<EtfsSnapshotDto> GetEtfsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the World snapshot. A dataset that has never loaded answers 503 with a
    /// <see cref="ResearchUnavailableDto"/>, which Refit raises as an <see cref="ApiException"/>.
    /// </summary>
    [Get("/api/Research/world")]
    Task<WorldSnapshotDto> GetWorldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets up to eight suggestions for the Companies tab's search box matching <paramref name="query"/> by ticker or
    /// company name. A rejected query answers 400, and a ticker list that has never loaded answers 503 with a
    /// <see cref="ResearchUnavailableDto"/>; Refit raises both as an <see cref="ApiException"/>.
    /// </summary>
    [Get("/api/Research/companies/suggest")]
    Task<List<CompanySuggestionDto>> SuggestCompaniesAsync([Query] string q,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a Company Profile by ticker: a profile, a fund, or a ticker SEC does not list. A rejected ticker
    /// answers 400, too many lookups 429, and SEC EDGAR being unreachable with no cached profile 503; Refit raises each
    /// as an <see cref="ApiException"/>.
    /// </summary>
    [Get("/api/Research/companies/{ticker}")]
    Task<CompanyLookupResultDto> GetCompanyAsync(string ticker, CancellationToken cancellationToken = default);
}
