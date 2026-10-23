using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Finance News page.
/// Delegates all business logic to <see cref="INewsService"/> and prepares view models.
/// </summary>
[Authorize]
public sealed class NewsController : Controller
{
    private readonly INewsService _newsService;
    private readonly ILogger<NewsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsController"/> class.
    /// </summary>
    /// <param name="newsService">Service for fetching and filtering finance news.</param>
    /// <param name="logger">Logger for this controller.</param>
    public NewsController(INewsService newsService, ILogger<NewsController> logger)
    {
        _newsService = newsService;
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
        var options = new NewsQueryOptions
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
            // Fetch all articles (unfiltered) to populate categories list
            List<NewsArticle> allArticles = await _newsService.GetFinanceNewsAsync(cancellationToken: cancellationToken);

            // Fetch filtered articles via the service
            List<NewsArticle> filteredArticles = await _newsService.GetFinanceNewsAsync(options, cancellationToken);

            model.Sources = _newsService.GetAvailableSources();
            model.Categories = allArticles.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();
            model.PersonalFinanceCategories = _newsService.GetPersonalFinanceCategories();
            model.Articles = filteredArticles;
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
        var options = new NewsQueryOptions
        {
            Category = category,
            Source = source,
            Search = search,
        };

        try
        {
            List<NewsArticle> articles = await _newsService.GetFinanceNewsAsync(options, cancellationToken);

            return Json(new NewsRefreshResponse
            {
                Success = true,
                Articles = articles,
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
