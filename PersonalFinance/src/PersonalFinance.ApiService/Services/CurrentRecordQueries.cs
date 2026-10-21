using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// The one definition of "the Account's current record" for settings that can have several rows, so the Dashboard,
/// the AI Assistant and any later reader show the same figures. Use with <c>FirstOrDefaultAsync</c>.
/// </summary>
public static class CurrentRecordQueries
{
    /// <summary>
    /// The Account's Net Worth parameters, most recently updated first.
    /// </summary>
    public static IQueryable<NetWorthParameters> CurrentFor(this IQueryable<NetWorthParameters> parameters,
        string userId)
    {
        return parameters
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ThenByDescending(p => p.Id);
    }

    /// <summary>
    /// The Account's Budget Planner allocation for its latest Period (<c>yyyy-MM</c>), latest first.
    /// </summary>
    public static IQueryable<UserBudgetAllocation> CurrentFor(this IQueryable<UserBudgetAllocation> allocations,
        string userId)
    {
        return allocations
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Period)
            .ThenByDescending(b => b.Id);
    }
}
