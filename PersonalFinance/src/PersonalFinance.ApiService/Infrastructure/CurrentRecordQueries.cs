using PersonalFinance.Data.Entities;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The one definition of "the Account's current record" for settings that can have several rows, and of the order an
/// Account's records are listed in, so the Dashboard, the AI Assistant and the feature pages show the same data.
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
    /// The Account's Savings Goals, newest first (deterministic: <c>Id</c> breaks ties).
    /// </summary>
    public static IQueryable<SavingsGoal> NewestFirstFor(this IQueryable<SavingsGoal> goals, string userId)
    {
        return goals
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id);
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
