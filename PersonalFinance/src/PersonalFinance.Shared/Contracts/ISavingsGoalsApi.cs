using Refit;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Savings Goals.
/// </summary>
public interface ISavingsGoalsApi
{
    /// <summary>
    /// Gets all savings goals for the current user.
    /// </summary>
    [Get("/api/SavingsGoals")]
    Task<List<SavingsGoalDto>> GetSavingsGoalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new savings goal for the current user.
    /// </summary>
    [Post("/api/SavingsGoals")]
    Task<CreateSavingsGoalResponseDto> CreateSavingsGoalAsync([Body] CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    [Put("/api/SavingsGoals/{id}")]
    Task<UpdateSavingsGoalResponseDto> UpdateSavingsGoalAsync(int id, [Body] UpdateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    [Delete("/api/SavingsGoals/{id}")]
    Task<DeleteSavingsGoalResponseDto> DeleteSavingsGoalAsync(int id, CancellationToken cancellationToken = default);
}
