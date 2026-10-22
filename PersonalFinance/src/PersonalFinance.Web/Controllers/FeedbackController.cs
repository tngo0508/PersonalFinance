using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Feedback;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models;

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

    /// <summary>
    /// The confirmation shown after Feedback is sent.
    /// </summary>
    public const string SentMessage = "Thanks! Your feedback was sent.";

    private readonly IFeedbackApi _feedbackApi;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(IFeedbackApi feedbackApi, ILogger<FeedbackController> logger)
    {
        _feedbackApi = feedbackApi;
        _logger = logger;
    }

    /// <summary>
    /// Shows the Send feedback form. <paramref name="from"/> is the page the Account came from; it is kept only
    /// when it is an app-relative path.
    /// </summary>
    [HttpGet]
    public IActionResult Index(string? from = null)
    {
        var pageUrl = AppRelativePath.IsValid(from) && from!.Length <= FeedbackLimits.PageUrlMaxLength ? from : null;
        return View(new FeedbackViewModel { PageUrl = pageUrl });
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
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Sending feedback failed.");
            ModelState.AddModelError(string.Empty,
                ApiErrorMessages.ForUser(ex, "Your feedback could not be sent. Please try again later."));
            return View(nameof(Index), model);
        }

        TempData[SentKey] = SentMessage;
        return RedirectToAction(nameof(Index));
    }
}
