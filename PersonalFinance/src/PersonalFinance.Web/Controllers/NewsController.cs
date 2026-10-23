using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Models.News;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Finance News page.
/// Reads the news feed from ApiService through <see cref="INewsApi"/> and prepares view models.
/// </summary>
[Authorize]
public sealed class NewsController : Controller
{
    private readonly INewsApi _newsApi;
    private readonly ILogger<NewsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsController"/> class.
    /// </summary>
    /// <param name="newsApi">ApiService client for the finance news feed.</param>
    /// <param name="logger">Logger for this controller.</param>
    public NewsController(INewsApi newsApi, ILogger<NewsController> logger)
    {
        _newsApi = newsApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Finance News dashboard with articles from multiple sources.
    /// Supports filtering by category, source, and search query.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] string? category,
        [FromQuery] string? source,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = new NewsQueryDto
        {
            Category = category,
            Source = source,
            Search = search,
        };

        var model = new NewsViewModel
        {
            SelectedCategory = category,
            SelectedSource = source,
            SearchQuery = search,
            IsLoading = true,
        };

        try
        {
            NewsFeedDto feed = await _newsApi.GetNewsAsync(query, cancellationToken);

            model.Sources = feed.Sources;
            model.Categories = feed.Categories;
            model.PersonalFinanceCategories = feed.PersonalFinanceCategories;
            model.Articles = feed.Articles;
            model.LastRefreshed = DateTime.UtcNow;
            model.IsLoading = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching finance news");
            model.ErrorMessage = "Unable to load finance news at this time. Please try again later.";
            model.IsLoading = false;
        }

        return View(model);
    }

    /// <summary>
    /// Returns JSON article data for AJAX-based page refresh.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> Refresh(
        [FromQuery] string? category,
        [FromQuery] string? source,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = new NewsQueryDto
        {
            Category = category,
            Source = source,
            Search = search,
        };

        try
        {
            NewsFeedDto feed = await _newsApi.GetNewsAsync(query, cancellationToken);

            return Json(new NewsRefreshResponse
            {
                Success = true,
                Articles = feed.Articles,
                LastRefreshed = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing finance news via API");

            return Json(new NewsRefreshResponse
            {
                Success = false,
                Error = "Failed to refresh news.",
            });
        }
    }
}
