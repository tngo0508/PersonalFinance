using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// Removes the Budget Tracker data. Every row gets the same <c>DeletedAt</c>, distinct from a Deletion Request's
/// stamp, so cancelling an account deletion does not bring removed data back.
/// </summary>
internal sealed class BudgetTrackerRemovalService : IBudgetTrackerRemovalService
{
    private readonly IBudgetDataSourceService _dataSourceService;
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public BudgetTrackerRemovalService(AppDbContext dbContext, IBudgetDataSourceService dataSourceService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _dataSourceService = dataSourceService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<BudgetDataSourceSummaryDto> RemoveAsync(string userId, CancellationToken ct = default)
    {
        List<BudgetDataSourceSummaryDto> summaries = await _dataSourceService.GetDataSummariesAsync(userId, ct);
        BudgetDataSourceSummaryDto removed = summaries.Single(s => s.Source == BudgetDataSource.BudgetTracker);

        DateTime stamp = _timeProvider.GetUtcNow().UtcDateTime;
        await SoftDeleteAllAsync(_dbContext.BudgetTransactions, userId, stamp, ct);
        await SoftDeleteAllAsync(_dbContext.MonthlyBudgetLines, userId, stamp, ct);
        await SoftDeleteAllAsync(_dbContext.MonthlyBudgetPlans, userId, stamp, ct);
        await SoftDeleteAllAsync(_dbContext.BudgetCategories, userId, stamp, ct);
        await _dbContext.SaveChangesAsync(ct);

        await _dataSourceService.ForgetRemovedSourceAsync(userId, BudgetDataSource.BudgetTracker, stamp, ct);
        return removed;
    }

    private static async Task SoftDeleteAllAsync<TEntity>(DbSet<TEntity> set, string userId, DateTime stamp,
        CancellationToken ct)
        where TEntity : AuditableEntity
    {
        List<TEntity> rows = await set.Where(e => e.UserId == userId).ToListAsync(ct);
        foreach (TEntity row in rows)
        {
            row.IsDeleted = true;
            row.DeletedAt = stamp;
        }
    }
}
