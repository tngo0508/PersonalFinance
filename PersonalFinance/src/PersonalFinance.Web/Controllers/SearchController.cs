using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// JSON endpoint behind the global search box in the app shell (<c>js/core/search.js</c>), forwarded to ApiService
/// through <see cref="ISearchApi"/>.
/// </summary>
[Authorize]
[JsonApiErrors]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : Controller
{
    private readonly ISearchApi _searchApi;

    public SearchController(ISearchApi searchApi)
    {
        _searchApi = searchApi;
    }

    /// <summary>
    /// Searches the signed-in Account's transactions, budget items and savings goals.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q, CancellationToken cancellationToken = default)
    {
        return Ok(await _searchApi.SearchAsync(q ?? string.Empty, cancellationToken));
    }
}
