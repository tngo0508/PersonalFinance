using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Watchlist;
using PersonalFinance.Web.Infrastructure;
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
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsController"/> class.
    /// </summary>
    /// <param name="newsApi">ApiService client for the finance news feed.</param>
    /// <param name="timeProvider">Clock for the page's last-refreshed time.</param>
    /// <param name="logger">Logger for this controller.</param>
    /// <param name="localizer">The page's text, in the request's Language.</param>
    public NewsController(INewsApi newsApi, TimeProvider timeProvider, ILogger<NewsController> logger,
        IStringLocalizer localizer)
    {
        _newsApi = newsApi;
        _timeProvider = timeProvider;
        _logger = logger;
        _localizer = localizer;
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
            NewsFeedDto feed = await GetNewsAsync(query, cancellationToken);

            model.Sources = feed.Sources;
            model.Topics = feed.Topics;
            model.Articles = feed.Articles;
            model.LastRefreshed = _timeProvider.GetUtcNow().UtcDateTime;
            model.IsLoading = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching finance news");
            model.ErrorMessage = _localizer["News.Message.LoadFailed"];
            model.IsLoading = false;
        }

        return View(model);
    }

    /// <summary>
    /// Reads the feed. When ApiService rejects the filters (for example, an overlong search), its 400 body is the feed
    /// with no articles and the filter options, so filters no article can match find nothing rather than showing the
    /// page as unavailable.
    /// </summary>
    private async Task<NewsFeedDto> GetNewsAsync(NewsQueryDto query, CancellationToken cancellationToken)
    {
        try
        {
            return await _newsApi.GetNewsAsync(query, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogInformation("ApiService rejected the news filters; showing no articles");
            return await ex.GetContentAsAsync<NewsFeedDto>() ?? new NewsFeedDto();
        }
    }
}
