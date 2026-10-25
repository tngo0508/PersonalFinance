using PersonalFinance.Shared.DTOs.BudgetTracker;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// The Data Source Removal of the Budget Tracker (GLOSSARY.md, ADR 0002 section 4).
/// </summary>
public interface IBudgetTrackerRemovalService
{
    /// <summary>
    /// Soft-deletes all of the user's categories, monthly plans and lines, and transactions, and returns what was
    /// removed. The default categories are seeded again the next time the Budget Tracker is opened.
    /// </summary>
    Task<BudgetDataSourceSummaryDto> RemoveAsync(string userId, CancellationToken ct = default);
}
