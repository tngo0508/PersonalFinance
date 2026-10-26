using PersonalFinance.Shared.DTOs.SavingsGoals;
using Refit;

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

    /// <summary>
    /// Lists a goal's Contributions, newest first.
    /// </summary>
    [Get("/api/SavingsGoals/{goalId}/contributions")]
    Task<List<ContributionDto>> GetContributionsAsync(int goalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a deposit (positive amount) or withdrawal (negative) on a goal.
    /// </summary>
    [Post("/api/SavingsGoals/{goalId}/contributions")]
    Task<SaveContributionResponseDto> AddContributionAsync(int goalId, [Body] SaveContributionRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a Contribution's amount, date or note.
    /// </summary>
    [Put("/api/SavingsGoals/{goalId}/contributions/{contributionId}")]
    Task<SaveContributionResponseDto> UpdateContributionAsync(int goalId, int contributionId,
        [Body] SaveContributionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a Contribution.
    /// </summary>
    [Delete("/api/SavingsGoals/{goalId}/contributions/{contributionId}")]
    Task<DeleteContributionResponseDto> DeleteContributionAsync(int goalId, int contributionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects a goal's saved amount to a figure, recording an Adjustment of the difference.
    /// </summary>
    [Post("/api/SavingsGoals/{goalId}/balance-correction")]
    Task<SaveContributionResponseDto> CorrectBalanceAsync(int goalId, [Body] CorrectBalanceRequestDto request,
        CancellationToken cancellationToken = default);
}
