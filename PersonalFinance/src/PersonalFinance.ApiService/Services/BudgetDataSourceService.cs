using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Services;

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
        var preference = await _dbContext.UserBudgetSourcePreferences
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

        var hasSheetData = await _dbContext.SpreadsheetBudgetSnapshots
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
        var preference = await GetDataSourcePreferenceAsync(userId, ct);
        return preference.Source;
    }

    /// <inheritdoc />
    public async Task<BudgetDataSourceDto> SetDataSourcePreferenceAsync(string userId,
        SetBudgetDataSourceRequest request, CancellationToken ct = default)
    {
        var preference = await _dbContext.UserBudgetSourcePreferences
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
                var entry = _dbContext.Entry(preference);
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
}