using PersonalFinance.Shared.DTOs.BudgetTracker;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Budget Tracker feature: monthly sheets, categories, transactions, and data source preference.
/// </summary>
public interface IBudgetTrackerApi
{
    /// <summary>
    /// Gets the monthly budget sheet (planned, actual, diff, totals, and transactions) for a given year and month.
    /// </summary>
    [Get("/api/budget-tracker/sheet")]
    Task<MonthlyBudgetSheetDto> GetSheetAsync([Query] int year, [Query] int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes a monthly budget plan (optionally copying planned amounts from the nearest earlier month).
    /// </summary>
    [Post("/api/budget-tracker/months")]
    Task<MonthlyBudgetSheetDto> InitializeMonthAsync([Body] InitializeMonthRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the planned amount for a category in a specific month and returns the updated sheet.
    /// </summary>
    [Put("/api/budget-tracker/months/{year}/{month}/lines/{categoryId}/planned")]
    Task<MonthlyBudgetSheetDto> SetPlannedAsync(int year, int month, int categoryId, [Body] SetPlannedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears a manual Actual override for a category in a specific month and returns the updated sheet.
    /// </summary>
    [Put("/api/budget-tracker/months/{year}/{month}/lines/{categoryId}/actual-override")]
    Task<MonthlyBudgetSheetDto> SetActualOverrideAsync(int year, int month, int categoryId,
        [Body] SetActualOverrideRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories for the current user.
    /// </summary>
    [Get("/api/budget-tracker/categories")]
    Task<List<BudgetCategoryDto>> GetCategoriesAsync([Query] bool includeArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new custom category.
    /// </summary>
    [Post("/api/budget-tracker/categories")]
    Task<BudgetCategoryDto> CreateCategoryAsync([Body] SaveCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing category (name, sort order).
    /// </summary>
    [Put("/api/budget-tracker/categories/{id}")]
    Task<BudgetCategoryDto> UpdateCategoryAsync(int id, [Body] SaveCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives (soft-deletes) a category.
    /// </summary>
    [Delete("/api/budget-tracker/categories/{id}")]
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Budget Tracker transaction.
    /// </summary>
    [Post("/api/budget-tracker/transactions")]
    Task<BudgetTransactionDto> CreateTransactionAsync([Body] SaveTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing Budget Tracker transaction.
    /// </summary>
    [Put("/api/budget-tracker/transactions/{id}")]
    Task<BudgetTransactionDto> UpdateTransactionAsync(int id, [Body] SaveTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes (soft-deletes) a Budget Tracker transaction.
    /// </summary>
    [Delete("/api/budget-tracker/transactions/{id}")]
    Task DeleteTransactionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's active budget data source preference.
    /// </summary>
    [Get("/api/budget-tracker/data-source")]
    Task<BudgetDataSourceDto> GetDataSourceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly sets the user's budget data source preference.
    /// </summary>
    [Put("/api/budget-tracker/data-source")]
    Task<BudgetDataSourceDto> SetDataSourceAsync([Body] SetBudgetDataSourceRequest request,
        CancellationToken cancellationToken = default);
}
