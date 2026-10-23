using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Features.NetWorth;

/// <summary>
/// Interface for persisting and retrieving user net worth projection parameters.
/// </summary>
public interface INetWorthParametersService
{
    /// <summary>
    /// Saves or updates the net worth projection parameters for a given user.
    /// </summary>
    Task<NetWorthParametersDto> SaveAsync(string userId, NetWorthProjectionRequestDto parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the saved net worth projection parameters for a given user.
    /// Returns null if no parameters exist.
    /// </summary>
    Task<NetWorthParametersDto?> LoadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the saved net worth projection parameters for a given user.
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
