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
}
