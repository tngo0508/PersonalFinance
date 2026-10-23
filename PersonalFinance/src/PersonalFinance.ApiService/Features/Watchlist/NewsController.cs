using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// API controller for the finance news feed that Watchlist articles are found in.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;
    private readonly IValidator<NewsQueryDto> _queryValidator;

    public NewsController(INewsService newsService, IValidator<NewsQueryDto> queryValidator)
    {
        _newsService = newsService;
        _queryValidator = queryValidator;
    }

    /// <summary>
    /// Gets the news articles matching the optional category, source and search filters, with the filter options.
    /// Rejected filters answer 400 with the reasons and the filter options but no articles, so the caller can still
    /// offer the filters without a second request.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get([FromQuery] NewsQueryDto query, CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _queryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            NewsFeedDto rejected = await _newsService.GetFilterOptionsAsync(cancellationToken);
            rejected.Errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(rejected);
        }

        NewsFeedDto feed = await _newsService.GetNewsAsync(query, cancellationToken);
        return Ok(feed);
    }
}
