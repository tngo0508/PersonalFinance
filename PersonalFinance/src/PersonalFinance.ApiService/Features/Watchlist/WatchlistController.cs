using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// API controller for watchlist (saved articles) endpoints.
/// All endpoints are scoped to the current user identified by the authenticated Account (see XUserIdAuthenticationHandler).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;
    private readonly IValidator<SaveArticleRequestDto> _saveValidator;

    public WatchlistController(IWatchlistService watchlistService, IValidator<SaveArticleRequestDto> saveValidator)
    {
        _watchlistService = watchlistService;
        _saveValidator = saveValidator;
    }

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        List<SavedArticleResponseDto> articles = await _watchlistService.GetWatchlistAsync(userId, cancellationToken);
        return Ok(articles);
    }

    /// <summary>
    /// Saves an article to the user's watchlist. An article with a missing or overlong field, or an address that is
    /// not http(s), answers 400 with the reasons and is not saved.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _saveValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        string userId = User.GetUserId();
        SaveArticleResultDto result = await _watchlistService.SaveArticleAsync(userId, request, cancellationToken);
        return Ok(new { success = result.Success, alreadySaved = result.AlreadySaved, id = result.Id });
    }

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveArticle(int id, CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        bool removed = await _watchlistService.RemoveArticleAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [HttpGet("check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckArticleSaved([FromQuery] string url,
        CancellationToken cancellationToken = default)
    {
        string userId = User.GetUserId();
        bool isSaved = await _watchlistService.IsArticleSavedAsync(userId, url, cancellationToken);
        return Ok(new { isSaved });
    }
}
