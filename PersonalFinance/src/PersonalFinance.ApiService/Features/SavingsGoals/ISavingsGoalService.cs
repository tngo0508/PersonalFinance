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
    /// Updates a goal's name, target, description or date. Its saved amount only changes through Contributions.
    /// </summary>
    Task<bool> UpdateSavingsGoalAsync(string userId, int id, UpdateSavingsGoalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a savings goal and its Contributions.
    /// </summary>
    Task<bool> DeleteSavingsGoalAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a goal's Contributions, newest first; null when the goal does not exist for this user.
    /// </summary>
    Task<List<ContributionDto>?> GetContributionsAsync(string userId, int goalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a deposit or withdrawal (a Regular Contribution), dated today unless the request gives a date.
    /// </summary>
    Task<ContributionResult> AddContributionAsync(string userId, int goalId, SaveContributionRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a Regular or Opening Contribution's amount, date (kept when the request has none) and note.
    /// </summary>
    Task<ContributionResult> UpdateContributionAsync(string userId, int goalId, int contributionId,
        SaveContributionRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a Contribution of any kind.
    /// </summary>
    Task<ContributionResult> DeleteContributionAsync(string userId, int goalId, int contributionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects a goal's saved amount to a figure by recording an Adjustment of the difference, or nothing when there
    /// is none.
    /// </summary>
    Task<ContributionResult> CorrectBalanceAsync(string userId, int goalId, CorrectBalanceRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Response DTO for a savings goal.
/// </summary>
public sealed class SavingsGoalResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The ISO 4217 currency of the goal's target, saved amount and Contributions (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The Account's Reporting Currency, which the reporting amounts below are in.</summary>
    public string ReportingCurrency { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }

    /// <summary><see cref="TargetAmount"/> in the Reporting Currency at the latest rate; null when no rate exists.</summary>
    public decimal? ReportingTargetAmount { get; set; }

    /// <summary><see cref="CurrentAmount"/> in the Reporting Currency at the latest rate; null when no rate exists.</summary>
    public decimal? ReportingCurrentAmount { get; set; }

    /// <summary>How the reporting amounts were converted, for the page's caption; null when none was needed.</summary>
    public PersonalFinance.Shared.DTOs.Currency.ConversionDto? Conversion { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public double ProgressPercent { get; set; }
    public List<ContributionDto> Contributions { get; set; } = [];
    public decimal SavedThisMonth { get; set; }

    /// <summary><see cref="SavedThisMonth"/> in the Reporting Currency at the latest rate; null when no rate exists.</summary>
    public decimal? ReportingSavedThisMonth { get; set; }

    public SavingsGoalPaceDto Pace { get; set; } = new();
}
