using Refit;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Type-safe Refit API contract shared between the backend ApiService and frontend Web project.
/// Refit automatically generates the HTTP client implementation at compile/runtime.
/// </summary>
public interface IItemsApi
{
    /// <summary>
    /// Retrieves all items from the ApiService backend.
    /// Accepts a CancellationToken to gracefully abort in-flight requests when the caller disconnects.
    /// </summary>
    [Get("/api/items")]
    Task<IEnumerable<ItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single item by its unique identifier.
    /// </summary>
    [Get("/api/items/{id}")]
    Task<ItemDto> GetItemAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new item on the backend database.
    /// </summary>
    [Post("/api/items")]
    Task<ItemDto> CreateItemAsync([Body] ItemDto item, CancellationToken cancellationToken = default);
}