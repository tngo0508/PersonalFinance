using Refit;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for net worth projection calculations.
/// </summary>
public interface INetWorthApi
{
    /// <summary>
    /// Computes a net worth projection for the given parameters and returns all three scenarios.
    /// </summary>
    [Post("/api/networth/calculate")]
    Task<NetWorthProjectionDto> CalculateProjectionAsync(
        [Body] NetWorthProjectionRequestDto request,
        CancellationToken cancellationToken = default);
}
