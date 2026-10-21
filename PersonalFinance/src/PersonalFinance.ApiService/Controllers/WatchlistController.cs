using PersonalFinance.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Controllers;

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

    public WatchlistController(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var articles = await _watchlistService.GetWatchlistAsync(userId, cancellationToken);
        return Ok(articles);
    }

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var result = await _watchlistService.SaveArticleAsync(userId, request, cancellationToken);
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
        var userId = User.GetUserId();
        var removed = await _watchlistService.RemoveArticleAsync(userId, id, cancellationToken);
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
        var userId = User.GetUserId();
        var isSaved = await _watchlistService.IsArticleSavedAsync(userId, url, cancellationToken);
        return Ok(new { isSaved });
    }
}
