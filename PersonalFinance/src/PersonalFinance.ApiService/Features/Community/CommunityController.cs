using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Community;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Community;

/// <summary>
/// RESTful API controller for the Guild Hall (the Community Feature): the Pseudonym, polls, Comments, Strategies and
/// Reactions. All endpoints are scoped to the current user identified by the authenticated Account (see
/// XUserIdAuthenticationHandler); the author of a post is always that Account. Reviewing Strategies and moderating
/// Comments require the Admin Policy.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CommunityController : ControllerBase
{
    private readonly ICommunityService _communityService;
    private readonly IValidator<CommunityStrategyRequestDto> _strategyValidator;
    private readonly IValidator<CommunityStrategyReviewRequestDto> _reviewValidator;
    private readonly IValidator<CommunityReactionRequestDto> _reactionValidator;

    public CommunityController(ICommunityService communityService,
        IValidator<CommunityStrategyRequestDto> strategyValidator,
        IValidator<CommunityStrategyReviewRequestDto> reviewValidator,
        IValidator<CommunityReactionRequestDto> reactionValidator)
    {
        _communityService = communityService;
        _strategyValidator = strategyValidator;
        _reviewValidator = reviewValidator;
        _reactionValidator = reactionValidator;
    }

    /// <summary>
    /// Gets the full Guild Hall data (the viewer's Pseudonym, polls, the board's Strategies, their Comments) for a user,
    /// with the board limited to <paramref name="category"/> when one is given.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommunityResponseDto>> GetCommunity(
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityResponseDto response = await _communityService.GetCommunityAsync(userId, category, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Replaces the user's Pseudonym with a new one.
    /// </summary>
    [HttpPost("pseudonym/reroll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommunityPseudonymDto>> RerollPseudonym(
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        return Ok(await _communityService.RerollPseudonymAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Votes on a poll option.
    /// </summary>
    [HttpPost("vote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityResponseDto>> Vote(
        [FromBody] CommunityVoteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        return Ok(await _communityService.VoteAsync(userId, request, cancellationToken));
    }

    /// <summary>
    /// Adds a comment to a poll or an Approved Strategy under the user's Pseudonym.
    /// </summary>
    [HttpPost("comment")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityCommentDto>> AddComment(
        [FromBody] CommunityCommentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        CommunityCommentDto comment = await _communityService.AddCommentAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCommunity), comment);
    }

    /// <summary>
    /// Gives or takes back a Reaction on an Approved Strategy or a shown Comment.
    /// </summary>
    [HttpPost("reactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommunityReactionResultDto>> ToggleReaction(
        [FromBody] CommunityReactionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _reactionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        return Ok(await _communityService.ToggleReactionAsync(User.GetUserId(), request, cancellationToken));
    }

    /// <summary>
    /// Pins a Strategy under the user's Pseudonym. It is stored Pending and only its author sees it until an Admin
    /// approves it. A money amount does not reject it; the Admin queue flags it.
    /// </summary>
    [HttpPost("strategies")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommunityStrategyDto>> SubmitStrategy(
        [FromBody] CommunityStrategyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _strategyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        CommunityStrategyDto strategy = await _communityService.SubmitStrategyAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetCommunity), strategy);
    }

    /// <summary>
    /// Lists the Strategies in <paramref name="status"/> for the Admin queue (Pending by default), oldest first.
    /// </summary>
    [HttpGet("strategies/review")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(List<CommunityStrategyReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CommunityStrategyReviewItemDto>>> ListStrategiesForReview(
        [FromQuery] CommunityStrategyStatus status = CommunityStrategyStatus.Pending,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ApiErrorException(ApiErrorKeys.Community.StrategyReviewStatus, "Unknown Strategy status.",
                nameof(status));
        }

        return Ok(await _communityService.ListStrategiesForReviewAsync(status, cancellationToken));
    }

    /// <summary>
    /// Approves or rejects a Strategy. Unknown (or soft-deleted) Strategies return 404.
    /// </summary>
    [HttpPut("strategies/{strategyId:int}/status")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewStrategy(int strategyId,
        [FromBody] CommunityStrategyReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _reviewValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        return await _communityService.ReviewStrategyAsync(strategyId, request.Status, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Lists the newest Comments for the Admin moderation list, hidden ones included.
    /// </summary>
    [HttpGet("comments/review")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(List<CommunityCommentReviewItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CommunityCommentReviewItemDto>>> ListCommentsForReview(
        CancellationToken cancellationToken = default)
    {
        return Ok(await _communityService.ListCommentsForReviewAsync(cancellationToken));
    }

    /// <summary>
    /// Hides a Comment or shows it again. Unknown (or soft-deleted) Comments return 404.
    /// </summary>
    [HttpPut("comments/{commentId:int}/visibility")]
    [Authorize(Policy = AppPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCommentVisibility(int commentId,
        [FromBody] CommunityCommentVisibilityRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await _communityService.SetCommentHiddenAsync(commentId, request.IsHidden, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
