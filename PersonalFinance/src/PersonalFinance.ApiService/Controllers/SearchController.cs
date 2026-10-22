using PersonalFinance.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for unified search endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Searches across transactions, Budget Categories, and Savings Goals.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var results = await _searchService.SearchAsync(userId, q, cancellationToken);
        return Ok(results);
    }
}
