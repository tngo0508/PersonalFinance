using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Web.Models;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Finance News page.
/// Aggregates finance news from multiple RSS sources and displays them in a card-based layout.
/// </summary>
[Authorize]
public class NewsController : Controller
{
    private readonly INewsService _newsService;
    private readonly ILogger<NewsController> _logger;

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
        var model = new NewsViewModel
        {
            SelectedCategory = category,
            SelectedSource = source,
            SearchQuery = search,
            IsLoading = true
        };

        try
        {
            var articles = await _newsService.GetFinanceNewsAsync(cancellationToken);
            model.Sources = _newsService.GetAvailableSources();
            model.Categories = articles.Select(a => a.Category).Distinct().OrderBy(c => c).ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                articles = articles.Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(source))
            {
                articles = articles.Where(a => a.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                var query = search.ToLowerInvariant();
                articles = articles.Where(a =>
                    a.Title.ToLowerInvariant().Contains(query) ||
                    a.Description.ToLowerInvariant().Contains(query) ||
                    a.Source.ToLowerInvariant().Contains(query)
                ).ToList();
            }

            model.Articles = articles;
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
    /// Returns JSON article data for AJAX-based refresh.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> Refresh(
        [FromQuery] string? category,
        [FromQuery] string? source,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var articles = await _newsService.GetFinanceNewsAsync(cancellationToken);

            if (!string.IsNullOrEmpty(category))
            {
                articles = articles.Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(source))
            {
                articles = articles.Where(a => a.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                var query = search.ToLowerInvariant();
                articles = articles.Where(a =>
                    a.Title.ToLowerInvariant().Contains(query) ||
                    a.Description.ToLowerInvariant().Contains(query) ||
                    a.Source.ToLowerInvariant().Contains(query)
                ).ToList();
            }

            return Json(new
            {
                success = true,
                articles = articles,
                lastRefreshed = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing finance news via API");
            return Json(new { success = false, error = "Failed to refresh news." });
        }
    }
}
