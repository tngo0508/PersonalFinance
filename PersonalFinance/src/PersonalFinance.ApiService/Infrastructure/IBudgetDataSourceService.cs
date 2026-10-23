using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
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
}
