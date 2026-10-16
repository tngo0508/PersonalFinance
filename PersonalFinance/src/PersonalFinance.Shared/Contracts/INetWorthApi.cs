using Refit;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for net worth projection calculations and parameter persistence.
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

    /// <summary>
    /// Saves or updates the net worth projection parameters for the authenticated user.
    /// </summary>
    [Post("/api/networth/parameters")]
    Task<NetWorthParametersDto> SaveParametersAsync(
        [Body] NetWorthProjectionRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the saved net worth projection parameters for the authenticated user.
    /// Returns null if no parameters exist.
    /// </summary>
    [Get("/api/networth/parameters")]
    Task<NetWorthParametersDto?> LoadParametersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the saved net worth projection parameters for the authenticated user.
    /// </summary>
    [Delete("/api/networth/parameters")]
    Task DeleteParametersAsync(
        CancellationToken cancellationToken = default);
}
