using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Feedback;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// MVC controller for the Send feedback page. Signed-out visitors are sent to sign in.
/// </summary>
[Authorize]
public class FeedbackController : Controller
{
    /// <summary>
    /// TempData key for the confirmation shown after Feedback is sent.
    /// </summary>
    public const string SentKey = "FeedbackSent";

    private readonly IFeedbackApi _feedbackApi;
    private readonly ILogger<FeedbackController> _logger;
    private readonly IStringLocalizer _localizer;

    public FeedbackController(IFeedbackApi feedbackApi, ILogger<FeedbackController> logger, IStringLocalizer localizer)
    {
        _feedbackApi = feedbackApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Shows the Send feedback form. <paramref name="from"/> is the page the Account came from; it is kept only
    /// when <see cref="FeedbackSourcePage.Accept"/> accepts it (app-relative, bounded, not this page).
    /// </summary>
    [HttpGet]
    public IActionResult Index(string? from = null)
    {
        return View(new FeedbackViewModel { PageUrl = FeedbackSourcePage.Accept(from) });
    }

    /// <summary>
    /// Sends the Feedback, then redirects back to the form with a confirmation (Post/Redirect/Get).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(FeedbackViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var request = new SubmitFeedbackRequest
        {
            Category = model.Category,
            Message = model.Message,
            PageUrl = model.PageUrl
        };

        try
        {
            await _feedbackApi.SubmitAsync(request, HttpContext.RequestAborted);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ModelState.AddModelError(string.Empty, _localizer["Feedback.Message.RateLimited"]);
            return View(nameof(Index), model);
        }
        catch (Exception ex) when ((ex is Refit.ApiException or HttpRequestException or TaskCanceledException)
                                   && !HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Sending feedback failed.");
            ModelState.AddModelError(string.Empty,
                ApiErrorMessages.ForUser(_localizer, ex, _localizer["Feedback.Message.SendFailed"]));
            return View(nameof(Index), model);
        }

        TempData[SentKey] = _localizer["Feedback.Message.Sent"].Value;
        return RedirectToAction(nameof(Index));
    }
}
