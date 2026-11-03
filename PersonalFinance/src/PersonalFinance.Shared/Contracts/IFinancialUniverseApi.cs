using PersonalFinance.Shared.DTOs.FinancialUniverse;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Financial Universe (spec #617): the graph of how the Account's finances connect.
/// </summary>
public interface IFinancialUniverseApi
{
    /// <summary>
    /// The graph for one month; the current month when <paramref name="year"/> and <paramref name="month"/> are null.
    /// </summary>
    [Get("/api/financial-universe")]
    Task<FinancialUniverseGraphDto> GetGraphAsync([Query] int? year = null, [Query] int? month = null,
        CancellationToken cancellationToken = default);
}
