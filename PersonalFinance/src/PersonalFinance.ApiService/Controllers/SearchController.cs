using PersonalFinance.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// API controller for unified search endpoints.
/// </summary>
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
    /// Searches across transactions, budgets, and savings goals.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var results = await _searchService.SearchAsync(userId, q, cancellationToken);
        return Ok(results);
    }
}
