using Microsoft.AspNetCore.Mvc;
using MediatR;
using PersonalFinance.ApiService.Watchlist;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Watchlist;

/// <summary>
/// API controller for watchlist (saved articles) endpoints.
/// All endpoints are scoped to the current user identified via the X-User-Id header forwarded by the Web proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WatchlistController : ControllerBase
{
    private readonly IMediator _mediator;

    public WatchlistController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the current user's ID from the X-User-Id header forwarded by the Web proxy.
    /// </summary>
    private string GetUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userId))
        {
            var value = userId.First();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return "anonymous";
    }

    /// <summary>
    /// Gets all saved articles for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var articles = await _mediator.Send(new GetWatchlistQuery { UserId = userId }, cancellationToken);
        return Ok(articles);
    }

    /// <summary>
    /// Saves an article to the user's watchlist.
    /// </summary>
    [HttpPost("watchlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _mediator.Send(new SaveArticleCommand { UserId = userId, Request = request }, cancellationToken);
        return Ok(new { success = result.Success, alreadySaved = result.AlreadySaved, id = result.Id });
    }

    /// <summary>
    /// Removes an article from the user's watchlist.
    /// </summary>
    [HttpDelete("watchlist/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveArticle(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var removed = await _mediator.Send(new RemoveArticleCommand { UserId = userId, Id = id }, cancellationToken);
        if (!removed) return NotFound();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Checks whether the current user has saved a specific article URL.
    /// </summary>
    [HttpGet("watchlist/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckArticleSaved([FromQuery] string url, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var isSaved = await _mediator.Send(new CheckArticleSavedQuery { UserId = userId, Url = url }, cancellationToken);
        return Ok(new { isSaved });
    }
}
