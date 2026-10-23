using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Features.NetWorth;

/// <summary>
/// Service responsible for persisting and retrieving user net worth projection parameters.
/// Encapsulates all database CRUD operations so the API controller remains thin.
/// </summary>
internal sealed class NetWorthParametersService : INetWorthParametersService
{
    private readonly AppDbContext _dbContext;

    public NetWorthParametersService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Saves or updates the net worth projection parameters for a given user.
    /// Creates a new record if one does not already exist.
    /// </summary>
    public async Task<NetWorthParametersDto> SaveAsync(string userId, NetWorthProjectionRequestDto parameters,
        CancellationToken cancellationToken = default)
    {
        // Include soft-deleted records to handle re-saving after deletion
        NetWorthParameters? existing = await _dbContext.NetWorthParameters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (existing != null)
        {
            // Reactivate if soft-deleted
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

            existing.Apply(parameters);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedAtUtc = existing.UpdatedAt.Value;
        }
        else
        {
            var entity = new NetWorthParameters { UserId = userId };
            entity.Apply(parameters);

            await _dbContext.NetWorthParameters.AddAsync(entity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (existing ?? _dbContext.NetWorthParameters.Local.First(e => e.UserId == userId)).ToDto();
    }

    /// <summary>
    /// Loads the saved net worth projection parameters for a given user.
    /// Returns null if no parameters exist.
    /// </summary>
    public async Task<NetWorthParametersDto?> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        NetWorthParameters? parameters = await _dbContext.NetWorthParameters
            .AsNoTracking()
            .CurrentFor(userId)
            .FirstOrDefaultAsync(cancellationToken);

        return parameters?.ToDto();
    }

    /// <summary>
    /// Soft-deletes the saved net worth projection parameters for a given user.
    /// </summary>
    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        NetWorthParameters? parameters = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (parameters != null)
        {
            parameters.SoftDelete();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
