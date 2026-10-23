using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Features.Watchlist;

/// <summary>
/// JSON endpoints behind the News page's article Watchlist, forwarded to ApiService through
/// <see cref="IWatchlistApi"/>. Writes require the antiforgery token (sent by <c>news.js</c>).
/// </summary>
[Authorize]
[JsonApiErrors]
[Route("api/[controller]")]
[Produces("application/json")]
public class WatchlistController : Controller
{
    private readonly IWatchlistApi _watchlistApi;

    public WatchlistController(IWatchlistApi watchlistApi)
    {
        _watchlistApi = watchlistApi;
    }

    /// <summary>
    /// Gets the signed-in Account's saved articles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        return Ok(await _watchlistApi.GetWatchlistAsync(cancellationToken));
    }

    /// <summary>
    /// Saves an article to the Watchlist.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveArticleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        SaveArticleResponseDto result = await _watchlistApi.SaveArticleAsync(request, cancellationToken);
        return Ok(new { success = result.Success, alreadySaved = result.AlreadySaved, id = result.Id });
    }

    /// <summary>
    /// Removes an article from the Watchlist.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken = default)
    {
        RemoveArticleResponseDto result = await _watchlistApi.RemoveArticleAsync(id, cancellationToken);
        return Ok(new { success = result.Success });
    }
}
