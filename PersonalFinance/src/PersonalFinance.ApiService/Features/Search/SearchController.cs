using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Search;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.Search;

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
        string userId = User.GetUserId();
        SearchResultsDto results = await _searchService.SearchAsync(userId, q, cancellationToken);
        return Ok(results);
    }
}
