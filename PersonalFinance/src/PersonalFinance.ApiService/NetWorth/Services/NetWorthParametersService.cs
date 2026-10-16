using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.NetWorth.Services;

/// <summary>
/// Service responsible for persisting and retrieving user net worth projection parameters.
/// Encapsulates all database CRUD operations so the API controller remains thin.
/// </summary>
public sealed class NetWorthParametersService : INetWorthParametersService
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
    public async Task<NetWorthParametersDto> SaveAsync(string userId, NetWorthProjectionRequestDto parameters, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (existing != null)
        {
            ApplyParameters(existing, parameters);
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var entity = new NetWorthParameters
            {
                UserId = userId,
                StartingBalance = parameters.StartingBalance,
                AnnualSavings = parameters.AnnualSavings,
                AnnualIncome = parameters.AnnualIncome,
                AnnualSpending = parameters.AnnualSpending,
                CurrentAge = parameters.CurrentAge,
                RetirementAge = parameters.RetirementAge,
                BaseRoi = parameters.BaseRoi,
                ConservativeRoi = parameters.ConservativeRoi,
                AggressiveRoi = parameters.AggressiveRoi,
                AggressiveSavings = parameters.AggressiveSavings
            };

            await _dbContext.NetWorthParameters.AddAsync(entity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(existing ?? _dbContext.NetWorthParameters.Local.FirstOrDefault(e => e.UserId == userId)!);
    }

    /// <summary>
    /// Loads the saved net worth projection parameters for a given user.
    /// Returns null if no parameters exist.
    /// </summary>
    public async Task<NetWorthParametersDto?> LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var parameters = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        return parameters is not null ? ToDto(parameters) : null;
    }

    /// <summary>
    /// Soft-deletes the saved net worth projection parameters for a given user.
    /// </summary>
    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var parameters = await _dbContext.NetWorthParameters
            .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        if (parameters != null)
        {
            parameters.SoftDelete();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ApplyParameters(NetWorthParameters entity, NetWorthProjectionRequestDto parameters)
    {
        entity.StartingBalance = parameters.StartingBalance;
        entity.AnnualSavings = parameters.AnnualSavings;
        entity.AnnualIncome = parameters.AnnualIncome;
        entity.AnnualSpending = parameters.AnnualSpending;
        entity.CurrentAge = parameters.CurrentAge;
        entity.RetirementAge = parameters.RetirementAge;
        entity.BaseRoi = parameters.BaseRoi;
        entity.ConservativeRoi = parameters.ConservativeRoi;
        entity.AggressiveRoi = parameters.AggressiveRoi;
        entity.AggressiveSavings = parameters.AggressiveSavings;
    }

    private static NetWorthParametersDto ToDto(NetWorthParameters entity)
    {
        return new NetWorthParametersDto
        {
            StartingBalance = entity.StartingBalance,
            AnnualSavings = entity.AnnualSavings,
            AnnualIncome = entity.AnnualIncome,
            AnnualSpending = entity.AnnualSpending,
            CurrentAge = entity.CurrentAge,
            RetirementAge = entity.RetirementAge,
            BaseRoi = entity.BaseRoi,
            ConservativeRoi = entity.ConservativeRoi,
            AggressiveRoi = entity.AggressiveRoi,
            AggressiveSavings = entity.AggressiveSavings,
            UpdatedAtUtc = entity.UpdatedAt ?? entity.CreatedAt
        };
    }
}
