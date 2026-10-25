using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Service implementation for managing user budget data source preferences.
/// </summary>
public class BudgetDataSourceService : IBudgetDataSourceService
{
    private readonly AppDbContext _dbContext;

    public BudgetDataSourceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public event Action<string>? PreferenceChanged;

    /// <inheritdoc />
    public async Task<BudgetDataSourceDto> GetDataSourcePreferenceAsync(string userId, CancellationToken ct = default)
    {
        UserBudgetSourcePreference? preference = await _dbContext.UserBudgetSourcePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (preference != null)
        {
            return new BudgetDataSourceDto
            {
                Source = preference.Source,
                IsDefault = false
            };
        }

        bool hasSheetData = await _dbContext.SpreadsheetBudgetSnapshots
                               .AsNoTracking()
                               .AnyAsync(s => s.UserId == userId, ct)
                           || await _dbContext.SpreadsheetTransactionRecords
                               .AsNoTracking()
                               .AnyAsync(t => t.UserId == userId, ct);

        return new BudgetDataSourceDto
        {
            Source = hasSheetData ? BudgetDataSource.GoogleSheet : BudgetDataSource.BudgetTracker,
            IsDefault = true
        };
    }

    /// <inheritdoc />
    public async Task<BudgetDataSource> GetActiveSourceAsync(string userId, CancellationToken ct = default)
    {
        BudgetDataSourceDto preference = await GetDataSourcePreferenceAsync(userId, ct);
        return preference.Source;
    }

    /// <inheritdoc />
    public async Task<BudgetDataSourceDto> SetDataSourcePreferenceAsync(string userId,
        SetBudgetDataSourceRequest request, CancellationToken ct = default)
    {
        UserBudgetSourcePreference? preference = await _dbContext.UserBudgetSourcePreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (preference == null)
        {
            preference = new UserBudgetSourcePreference
            {
                UserId = userId,
                Source = request.Source
            };
            _dbContext.UserBudgetSourcePreferences.Add(preference);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                EntityEntry<UserBudgetSourcePreference> entry = _dbContext.Entry(preference);
                if (entry.State != EntityState.Detached)
                {
                    entry.State = EntityState.Detached;
                }

                preference = await _dbContext.UserBudgetSourcePreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId, ct);

                if (preference != null)
                {
                    preference.Source = request.Source;
                    preference.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(ct);
                }
                else
                {
                    throw;
                }
            }
        }
        else
        {
            preference.Source = request.Source;
            preference.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        PreferenceChanged?.Invoke(userId);

        return new BudgetDataSourceDto
        {
            Source = preference.Source,
            IsDefault = false
        };
    }

    /// <inheritdoc />
    public async Task<List<BudgetDataSourceSummaryDto>> GetDataSummariesAsync(string userId,
        CancellationToken ct = default)
    {
        return
        [
            new BudgetDataSourceSummaryDto
            {
                Source = BudgetDataSource.GoogleSheet,
                MonthCount = await SheetMonthsQuery(userId).CountAsync(ct),
                TransactionCount = await _dbContext.SpreadsheetTransactionRecords
                    .AsNoTracking()
                    .CountAsync(t => t.UserId == userId, ct)
            },
            new BudgetDataSourceSummaryDto
            {
                Source = BudgetDataSource.BudgetTracker,
                MonthCount = await TrackerMonthsQuery(userId).CountAsync(ct),
                TransactionCount = await _dbContext.BudgetTransactions
                    .AsNoTracking()
                    .CountAsync(t => t.UserId == userId, ct)
            }
        ];
    }

    /// <inheritdoc />
    public async Task ForgetRemovedSourceAsync(string userId, BudgetDataSource removedSource, DateTime deletedAt,
        CancellationToken ct = default)
    {
        UserBudgetSourcePreference? preference = await _dbContext.UserBudgetSourcePreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (preference != null && preference.Source == removedSource)
        {
            preference.IsDeleted = true;
            preference.DeletedAt = deletedAt;
            await _dbContext.SaveChangesAsync(ct);
        }

        PreferenceChanged?.Invoke(userId);
    }

    /// <summary>The user's distinct synced months. Internal so tests can check it translates to Azure SQL.</summary>
    internal IQueryable<int> SheetMonthsQuery(string userId) =>
        _dbContext.SpreadsheetBudgetSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Year * 100 + s.MonthNumber)
            .Distinct();

    /// <summary>
    /// The user's distinct Budget Tracker months: months with a plan or with a transaction dated in them.
    /// Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<int> TrackerMonthsQuery(string userId) =>
        _dbContext.MonthlyBudgetPlans
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Year * 100 + p.Month)
            .Union(_dbContext.BudgetTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => t.Date.Year * 100 + t.Date.Month));
}
