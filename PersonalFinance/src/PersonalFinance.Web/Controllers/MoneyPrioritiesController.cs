using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.MoneyPriorities;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for Money Priorities. Renders the page, then answers its background calls (submit, view, delete,
/// Earlier results) through the <see cref="IMoneyPrioritiesApi"/> Refit client. The ApiService computes the path and
/// every figure; this controller only passes them on.
/// </summary>
[Authorize]
public class MoneyPrioritiesController : Controller
{
    private const string PageTitle = "Money Priorities";

    private readonly IMoneyPrioritiesApi _moneyPrioritiesApi;
    private readonly ILogger<MoneyPrioritiesController> _logger;

    public MoneyPrioritiesController(IMoneyPrioritiesApi moneyPrioritiesApi, ILogger<MoneyPrioritiesController> logger)
    {
        _moneyPrioritiesApi = moneyPrioritiesApi;
        _logger = logger;
    }

    /// <summary>
    /// Displays the page: the unhighlighted Priority Tree, Earlier results, and the form pre-filled from the latest
    /// Walk. Shows the Unavailable State when any of them cannot be loaded.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new MoneyPrioritiesViewModel();
        try
        {
            PriorityTreeDiagramDto tree = await _moneyPrioritiesApi.GetTreeAsync(cancellationToken);
            model.WholeTreeDiagram = tree.WholeTreeDiagram;
            model.RecentWalks = await _moneyPrioritiesApi.GetRecentWalksAsync(cancellationToken);
            if (model.RecentWalks.Count > 0)
            {
                PriorityWalkResultDto latest =
                    await _moneyPrioritiesApi.GetWalkAsync(model.RecentWalks[0].WalkId, cancellationToken);
                model.LatestAnswers = latest.Answers;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load Money Priorities; rendering the Unavailable State instead.");
            return this.UnavailableView(PageTitle, "We couldn't load Money Priorities", "MoneyPriorities");
        }

        return View(model);
    }

    /// <summary>
    /// Submits the answers as one Priority Walk and returns its result. A rejected submit answers 400 with the
    /// ApiService's messages keyed by field, so the page can show each one under its field.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [JsonApiErrors]
    public async Task<IActionResult> Submit([FromBody] SubmitPriorityWalkRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new MoneyPrioritiesValidationErrorsDto
            {
                Errors = ["Answer the questions, then try again."]
            });
        }

        try
        {
            PriorityWalkResultDto result = await _moneyPrioritiesApi.SubmitWalkAsync(request, cancellationToken);
            return Json(result);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return BadRequest(await ValidationErrorsAsync(ex));
        }
    }

    /// <summary>
    /// The ApiService's field-keyed validation errors. Any other 400 body (say, a model-binding problem) becomes one
    /// general message, never its raw text.
    /// </summary>
    private async Task<MoneyPrioritiesValidationErrorsDto> ValidationErrorsAsync(ApiException ex)
    {
        MoneyPrioritiesValidationErrorsDto? errors = null;
        try
        {
            errors = await ex.GetContentAsAsync<MoneyPrioritiesValidationErrorsDto>();
        }
        catch (JsonException)
        {
            _logger.LogWarning("ApiService rejected a Money Priorities submit with a body that is not field errors.");
        }

        errors ??= new MoneyPrioritiesValidationErrorsDto();
        if (errors.Errors.Count == 0)
        {
            errors.FieldErrors.Clear();
            errors.Errors.Add("Check your answers, then try again.");
        }

        return errors;
    }

    /// <summary>Returns one earlier Priority Walk with its figures recomputed (404 when the Account has none).</summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Walk(int id, CancellationToken cancellationToken = default)
    {
        PriorityWalkResultDto walk = await _moneyPrioritiesApi.GetWalkAsync(id, cancellationToken);
        return Json(walk);
    }

    /// <summary>Removes one earlier Priority Walk (soft delete in the ApiService).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [JsonApiErrors]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _moneyPrioritiesApi.DeleteWalkAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns Earlier results: the Account's 10 newest Priority Walks, newest first.</summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken = default)
    {
        List<PriorityWalkSummaryDto> walks = await _moneyPrioritiesApi.GetRecentWalksAsync(cancellationToken);
        return Json(walks);
    }
}
