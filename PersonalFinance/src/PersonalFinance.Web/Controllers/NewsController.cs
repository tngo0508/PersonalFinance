using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Models.News;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for the Finance News page.
/// Reads the news feed from ApiService through <see cref="INewsApi"/> and prepares view models.
/// </summary>
[Authorize]
public sealed class NewsController : Controller
{
    private readonly INewsApi _newsApi;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NewsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsController"/> class.
    /// </summary>
    /// <param name="newsApi">ApiService client for the finance news feed.</param>
    /// <param name="timeProvider">Clock for the page's last-refreshed time.</param>
    /// <param name="logger">Logger for this controller.</param>
    public NewsController(INewsApi newsApi, TimeProvider timeProvider, ILogger<NewsController> logger)
    {
        _newsApi = newsApi;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Displays the Finance News dashboard with articles from multiple sources.
    /// Supports filtering by category, source, and search query.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] NewsQueryDto query, CancellationToken cancellationToken = default)
    {
        var model = new NewsViewModel
        {
            SelectedCategory = query.Category,
            SelectedSource = query.Source,
            SearchQuery = query.Search,
            IsLoading = true,
        };

        try
        {
            NewsFeedDto feed = await TryGetNewsAsync(query, cancellationToken)
                ?? await GetFilterOptionsWithoutArticlesAsync(cancellationToken);

            model.Sources = feed.Sources;
            model.Categories = feed.Categories;
            model.PersonalFinanceCategories = feed.PersonalFinanceCategories;
            model.Articles = feed.Articles;
            model.LastRefreshed = _timeProvider.GetUtcNow().UtcDateTime;
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
    public async Task<IActionResult> Refresh([FromQuery] NewsQueryDto query, CancellationToken cancellationToken = default)
    {
        try
        {
            NewsFeedDto? feed = await TryGetNewsAsync(query, cancellationToken);

            return Json(new NewsRefreshResponse
            {
                Success = true,
                Articles = feed?.Articles ?? [],
                LastRefreshed = _timeProvider.GetUtcNow().UtcDateTime,
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

    /// <summary>
    /// Reads the feed, or returns <see langword="null"/> when ApiService rejects the filters (for example, an overlong
    /// search): filters no article can match find nothing, rather than showing the page as unavailable.
    /// </summary>
    private async Task<NewsFeedDto?> TryGetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken)
    {
        try
        {
            return await _newsApi.GetNewsAsync(query, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogInformation("ApiService rejected the news filters; showing no articles");
            return null;
        }
    }

    /// <summary>
    /// Reads the unfiltered feed for its filter options, so the page can still offer them, and drops its articles.
    /// </summary>
    private async Task<NewsFeedDto> GetFilterOptionsWithoutArticlesAsync(CancellationToken cancellationToken)
    {
        NewsFeedDto feed = await _newsApi.GetNewsAsync(new NewsQueryDto(), cancellationToken);
        feed.Articles = [];
        return feed;
    }
}
