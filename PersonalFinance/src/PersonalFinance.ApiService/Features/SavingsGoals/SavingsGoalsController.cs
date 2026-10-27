using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.SavingsGoals;

/// <summary>
/// API controller for savings goals endpoints.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SavingsGoalsController : ControllerBase
{
    private readonly ISavingsGoalService _savingsGoalService;
    private readonly IValidator<CreateSavingsGoalRequestDto> _createValidator;
    private readonly IValidator<SaveContributionRequestDto> _contributionValidator;
    private readonly IValidator<CorrectBalanceRequestDto> _correctBalanceValidator;

    public SavingsGoalsController(ISavingsGoalService savingsGoalService,
        IValidator<CreateSavingsGoalRequestDto> createValidator,
        IValidator<SaveContributionRequestDto> contributionValidator,
        IValidator<CorrectBalanceRequestDto> correctBalanceValidator)
    {
        _savingsGoalService = savingsGoalService;
        _createValidator = createValidator;
        _contributionValidator = contributionValidator;
        _correctBalanceValidator = correctBalanceValidator;
    }

    /// <summary>
    /// Gets all savings goals for the current user, each with its Contributions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSavingsGoals(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<SavingsGoalResponseDto> goals = await _savingsGoalService.GetSavingsGoalsAsync(userId, cancellationToken);
        return Ok(goals);
    }

    /// <summary>
    /// Creates a new savings goal for the current user. A starting amount becomes its Opening Contribution.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateSavingsGoal([FromBody] CreateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        string userId = User.GetUserId();
        int id = await _savingsGoalService.CreateSavingsGoalAsync(userId, request, cancellationToken);
        return Ok(new { success = true, id });
    }

    /// <summary>
    /// Updates a goal's name, target, description or date. The saved amount only changes through Contributions.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        bool updated = await _savingsGoalService.UpdateSavingsGoalAsync(userId, id, request, cancellationToken);
        if (!updated) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a savings goal and its Contributions.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavingsGoal(int id, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        bool deleted = await _savingsGoalService.DeleteSavingsGoalAsync(userId, id, cancellationToken);
        if (!deleted) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Lists a goal's Contributions, newest first.
    /// </summary>
    [HttpGet("{goalId}/contributions")]
    [ProducesResponseType(typeof(List<ContributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContributions(int goalId, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<ContributionDto>? contributions =
            await _savingsGoalService.GetContributionsAsync(userId, goalId, cancellationToken);
        if (contributions is null) return NotFound();
        return Ok(contributions);
    }

    /// <summary>
    /// Records a deposit (positive amount) or withdrawal (negative) on a goal.
    /// </summary>
    [HttpPost("{goalId}/contributions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddContribution(int goalId, [FromBody] SaveContributionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _contributionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        string userId = User.GetUserId();
        return ToResponse(await _savingsGoalService.AddContributionAsync(userId, goalId, request, cancellationToken));
    }

    /// <summary>
    /// Edits a Contribution's amount, date or note. An Adjustment can only be deleted.
    /// </summary>
    [HttpPut("{goalId}/contributions/{contributionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateContribution(int goalId, int contributionId,
        [FromBody] SaveContributionRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _contributionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        string userId = User.GetUserId();
        return ToResponse(await _savingsGoalService.UpdateContributionAsync(userId, goalId, contributionId, request,
            cancellationToken));
    }

    /// <summary>
    /// Deletes a Contribution.
    /// </summary>
    [HttpDelete("{goalId}/contributions/{contributionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteContribution(int goalId, int contributionId,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        return ToResponse(await _savingsGoalService.DeleteContributionAsync(userId, goalId, contributionId,
            cancellationToken));
    }

    /// <summary>
    /// Corrects a goal's saved amount to a figure, recording an Adjustment of the difference.
    /// </summary>
    [HttpPost("{goalId}/balance-correction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CorrectBalance(int goalId, [FromBody] CorrectBalanceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _correctBalanceValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        string userId = User.GetUserId();
        return ToResponse(await _savingsGoalService.CorrectBalanceAsync(userId, goalId, request, cancellationToken));
    }

    /// <summary>
    /// Maps a Contribution result to its response. A change that would leave the saved amount below zero is a 409 and
    /// an edit to an Adjustment a 400, each carrying its <see cref="ApiErrorKeys.SavingsGoals"/> key (ADR 0017).
    /// </summary>
    private IActionResult ToResponse(ContributionResult result) => result.Outcome switch
    {
        ContributionOutcome.Saved => Ok(new { success = true, id = result.ContributionId }),
        ContributionOutcome.NotFound => NotFound(),
        ContributionOutcome.BelowZero => throw new ApiErrorException(ApiErrorKeys.SavingsGoals.BelowZero,
            "This change would leave the goal's saved amount below zero.",
            statusCode: StatusCodes.Status409Conflict),
        ContributionOutcome.NotEditable => throw new ApiErrorException(
            ApiErrorKeys.SavingsGoals.AdjustmentNotEditable, "A balance adjustment can only be deleted."),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown Contribution outcome.")
    };
}
