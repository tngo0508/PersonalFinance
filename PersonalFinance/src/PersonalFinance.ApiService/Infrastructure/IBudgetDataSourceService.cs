using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Service interface for resolving and setting user budget data source preferences.
/// </summary>
public interface IBudgetDataSourceService
{
    /// <summary>
    /// Event triggered when a user's data source preference is changed.
    /// </summary>
    event Action<string>? PreferenceChanged;

    /// <summary>
    /// Gets the user's active budget data source preference (resolving the default if unset).
    /// </summary>
    Task<BudgetDataSourceDto> GetDataSourcePreferenceAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Explicitly saves the user's budget data source preference.
    /// </summary>
    Task<BudgetDataSourceDto> SetDataSourcePreferenceAsync(string userId, SetBudgetDataSourceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the currently active budget data source for the user.
    /// </summary>
    Task<BudgetDataSource> GetActiveSourceAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Counts the user's months and transactions in each Data Source, Google Sheet first.
    /// </summary>
    Task<List<BudgetDataSourceSummaryDto>> GetDataSummariesAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// After a Data Source Removal: soft-deletes the stored preference if it points at the removed source, so the
    /// read-time default picks the source again (ADR 0002), and raises <see cref="PreferenceChanged"/> either way.
    /// </summary>
    Task ForgetRemovedSourceAsync(string userId, BudgetDataSource removedSource, DateTime deletedAt,
        CancellationToken ct = default);
}
