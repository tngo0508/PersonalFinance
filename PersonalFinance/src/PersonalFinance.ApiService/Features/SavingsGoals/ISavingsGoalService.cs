using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// Service for managing user savings goals.
/// </summary>
public interface ISavingsGoalService
{
    /// <summary>
    /// Gets all savings goals for a user.
    /// </summary>
    Task<List<SavingsGoalResponseDto>> GetSavingsGoalsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new savings goal for a user.
    /// </summary>
    Task<int> CreateSavingsGoalAsync(string userId, CreateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing savings goal.
    /// </summary>
    Task<bool> UpdateSavingsGoalAsync(string userId, int id, UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a savings goal.
    /// </summary>
    Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response DTO for a savings goal.
/// </summary>
public sealed class SavingsGoalResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public double ProgressPercent { get; set; }
}
