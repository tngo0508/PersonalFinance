using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// Service interface for Monthly Budget Tracker operations including sheets, categories, transactions, and data source preferences.
/// </summary>
public interface IMonthlyBudgetService
{
    /// <summary>
    /// Seeds default expense and income categories for a user if they do not yet have any.
    /// </summary>
    Task EnsureDefaultCategoriesAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Computes and returns the complete budget sheet for a given user, year, and month.
    /// </summary>
    Task<MonthlyBudgetSheetDto> GetSheetAsync(string userId, int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Initializes a monthly budget plan, optionally copying planned amounts from the nearest earlier month with a plan.
    /// </summary>
    Task<MonthlyBudgetSheetDto> InitializeMonthAsync(string userId, InitializeMonthRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Sets the planned amount for a specific category in a month.
    /// </summary>
    Task<MonthlyBudgetSheetDto> SetPlannedAsync(string userId, int year, int month, int categoryId,
        SetPlannedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sets or clears a manual Actual override for a category in a month. Passing null reverts to transaction sum.
    /// </summary>
    Task<MonthlyBudgetSheetDto> SetActualOverrideAsync(string userId, int year, int month, int categoryId,
        SetActualOverrideRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all categories for the user.
    /// </summary>
    Task<List<BudgetCategoryDto>> GetCategoriesAsync(string userId, bool includeArchived = false,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new custom budget category for the user.
    /// </summary>
    Task<BudgetCategoryDto> CreateCategoryAsync(string userId, SaveCategoryRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing category (name, sort order). Category kind is immutable.
    /// </summary>
    Task<BudgetCategoryDto> UpdateCategoryAsync(string userId, int id, SaveCategoryRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Archives (soft-deletes) a category.
    /// </summary>
    Task DeleteCategoryAsync(string userId, int id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new transaction.
    /// </summary>
    Task<BudgetTransactionDto> CreateTransactionAsync(string userId, SaveTransactionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    Task<BudgetTransactionDto> UpdateTransactionAsync(string userId, int id, SaveTransactionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes (soft-deletes) a transaction.
    /// </summary>
    Task DeleteTransactionAsync(string userId, int id, CancellationToken ct = default);
}
