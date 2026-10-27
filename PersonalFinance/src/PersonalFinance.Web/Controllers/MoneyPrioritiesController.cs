using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.MoneyPriorities;
using PersonalFinance.Web.Services;
using Refit;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC Controller for Money Priorities. Renders the page, then answers its background calls (submit, view, delete,
/// Earlier results, the Purpose Note) through the <see cref="IMoneyPrioritiesApi"/> Refit client. The ApiService computes the path and
/// every figure; this controller only passes them on.
/// </summary>
[Authorize]
public class MoneyPrioritiesController : Controller
{
    private readonly IMoneyPrioritiesApi _moneyPrioritiesApi;
    private readonly ILogger<MoneyPrioritiesController> _logger;
    private readonly IStringLocalizer _localizer;
    private readonly IUserPreferenceService _preferences;

    public MoneyPrioritiesController(IMoneyPrioritiesApi moneyPrioritiesApi, ILogger<MoneyPrioritiesController> logger,
        IStringLocalizer localizer, IUserPreferenceService preferences)
    {
        _moneyPrioritiesApi = moneyPrioritiesApi;
        _logger = logger;
        _localizer = localizer;
        _preferences = preferences;
    }

    /// <summary>
    /// Displays the page: the unhighlighted Priority Tree, Earlier results, and the form pre-filled from the latest
    /// Walk. Shows the Unavailable State when any of them cannot be loaded.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var model = new MoneyPrioritiesViewModel
        {
            PurposeNoteDisclaimer = _localizer["MoneyPriorities.Note.Disclaimer"],
            PurposeNoteUnavailable = _localizer["MoneyPriorities.Note.Unavailable"]
        };
        try
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            model.CurrencyCode = userId is null
                ? SupportedCurrencies.DefaultCode
                : await _preferences.GetReportingCurrencyAsync(userId, cancellationToken);
            PriorityTreeDiagramDto tree = await _moneyPrioritiesApi.GetTreeAsync(cancellationToken);
            model.WholeTreeDiagram = MoneyPrioritiesText.WholeTreeDiagram(_localizer, tree.Tree);
            model.Thresholds = tree.Thresholds;
            model.RecentWalks = MoneyPrioritiesText.Localize(_localizer,
                await _moneyPrioritiesApi.GetRecentWalksAsync(cancellationToken));
            if (model.RecentWalks.Count > 0)
            {
                PriorityWalkResultDto latest =
                    await _moneyPrioritiesApi.GetWalkAsync(model.RecentWalks[0].WalkId, cancellationToken);
                model.LatestAnswers = latest.Answers;
                model.CurrencyCode = SupportedCurrencies.CodeOrDefault(latest.CurrencyCode);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load Money Priorities; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["MoneyPriorities.Title"], _localizer["MoneyPriorities.Message.Unavailable"],
                "MoneyPriorities");
        }

        return View(model);
    }

    /// <summary>
    /// Submits the answers as one Priority Walk and returns its result. A rejected submit answers 400 with the
    /// ApiService's messages in the request's Language, keyed by field, so the page can show each one under its field.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [JsonApiErrors]
    public async Task<IActionResult> Submit([FromBody] SubmitPriorityWalkRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new MoneyPrioritiesErrorsViewModel
            {
                Errors = [_localizer["MoneyPriorities.Message.NoAnswers"]]
            });
        }

        try
        {
            PriorityWalkResultDto result = await _moneyPrioritiesApi.SubmitWalkAsync(request, cancellationToken);
            return Json(MoneyPrioritiesText.Result(_localizer, result));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return BadRequest(Errors(ex));
        }
    }

    /// <summary>
    /// The ApiService's keyed validation errors in the request's Language: whole-request messages in
    /// <see cref="MoneyPrioritiesErrorsViewModel.Errors"/>, the rest under their field. Any other 400 body (say, a
    /// model-binding problem) becomes one general message, never its raw text.
    /// </summary>
    private MoneyPrioritiesErrorsViewModel Errors(ApiException ex)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> fields = ApiErrorMessages.Fields(_localizer, ex);
        if (fields.Count == 0)
        {
            _logger.LogWarning("ApiService rejected a Money Priorities submit with a body that is not keyed errors.");
            return new MoneyPrioritiesErrorsViewModel { Errors = [_localizer["MoneyPriorities.Message.CheckAnswers"]] };
        }

        var errors = new MoneyPrioritiesErrorsViewModel();
        foreach ((string field, IReadOnlyList<string> messages) in fields)
        {
            if (field.Length == 0)
            {
                errors.Errors.AddRange(messages);
            }
            else
            {
                errors.FieldErrors[field] = [.. messages];
            }
        }

        return errors;
    }

    /// <summary>Returns one earlier Priority Walk with its figures recomputed (404 when the Account has none).</summary>
    [HttpGet]
    [JsonApiErrors]
    public async Task<IActionResult> Walk(int id, CancellationToken cancellationToken = default)
    {
        PriorityWalkResultDto walk = await _moneyPrioritiesApi.GetWalkAsync(id, cancellationToken);
        return Json(MoneyPrioritiesText.Result(_localizer, walk));
    }

    /// <summary>
    /// Writes (or reuses) a Priority Walk's Purpose Note. The page asks for it after the result shows; every outcome
    /// (written, no Purpose, not available, quota spent, refused) is a 200 whose state says which. 404 when the Account
    /// has no Walk with that id.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [JsonApiErrors]
    public async Task<IActionResult> Note(int id, CancellationToken cancellationToken = default)
    {
        PriorityWalkPurposeNoteDto note = await _moneyPrioritiesApi.WritePurposeNoteAsync(id, cancellationToken);
        return Json(MoneyPrioritiesText.Localize(_localizer, note));
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
        return Json(MoneyPrioritiesText.Localize(_localizer, walks));
    }
}
