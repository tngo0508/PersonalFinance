using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Learning;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Learning;
using ApiException = Refit.ApiException;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// The Atelier's pages (#513): the Journey's road and the Library, each Term Entry and each Lesson, plus the
/// <c>robots.txt</c> and <c>/sitemap.xml</c> that let a search engine find the entries. Every page is readable without
/// signing in; a guest sees the Journey's outline and a sign-in prompt, and only finishing a Lesson needs the Account.
/// Content and progress come from ApiService through <see cref="ILearningApi"/> in the request's Language.
/// </summary>
public sealed class LearningController : Controller
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly ILearningApi _learningApi;
    private readonly ILogger<LearningController> _logger;
    private readonly IStringLocalizer _localizer;

    public LearningController(ILearningApi learningApi, ILogger<LearningController> logger,
        IStringLocalizer localizer)
    {
        _learningApi = learningApi;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// The Journey's road (the signed-in Account's progress, or the outline for a guest) and the Library: book spines
    /// on a shelf per Topic, or a "being written" note while it is empty.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            string language = AppLanguages.Current().Code;
            LearningLibraryDto library = await _learningApi.GetLibraryAsync(language, cancellationToken);
            JourneyStateDto? journey = IsSignedIn ? await _learningApi.GetJourneyAsync(language, cancellationToken) : null;
            IReadOnlyList<LearningRoadStop> road = journey is not null
                ? LearningRoadStop.ForJourney(journey)
                : LearningRoadStop.Outline(await _learningApi.GetLessonsAsync(language, cancellationToken));
            return View(new LearningIndexViewModel
            {
                Topics = library.Topics,
                Road = road,
                IsSignedIn = journey is not null,
                ShowOnDashboard = journey?.ShowOnDashboard ?? false
            });
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Library; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["Learning.Title"], _localizer["Learning.Message.Unavailable"],
                "Learning");
        }
    }

    /// <summary>A Term Entry; an unknown slug is a 404.</summary>
    [AllowAnonymous]
    [HttpGet("/Learning/Term/{slug}")]
    public async Task<IActionResult> Term(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            TermEntryDto entry = await _learningApi.GetTermAsync(slug, AppLanguages.Current().Code, cancellationToken);
            return View(new LearningTermViewModel
            {
                Entry = entry,
                CanonicalUrl = TermUrl(entry.Slug),
            });
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load a Term Entry; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["Learning.Title"], _localizer["Learning.Message.TermUnavailable"],
                "Learning", nameof(Term), new Dictionary<string, string> { ["slug"] = slug });
        }
    }

    /// <summary>
    /// A Lesson: stacked paper sheets and the Check when it is open or finished, its opening when it is locked, and
    /// the title with a sign-in prompt for a guest. An unknown slug is a 404.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/Learning/Lesson/{slug}")]
    public async Task<IActionResult> Lesson(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            string language = AppLanguages.Current().Code;
            LessonDto lesson = await _learningApi.GetLessonAsync(slug, language, cancellationToken);
            if (!IsSignedIn)
            {
                return View(LearningLessonViewModel.ForGuest(lesson));
            }

            JourneyStateDto journey = await _learningApi.GetJourneyAsync(language, cancellationToken);
            IReadOnlyList<ReviewCardDto> review = LearningLessonViewModel.ReviewIsDue(lesson, journey)
                ? await ReviewOrNoneAsync(language, cancellationToken)
                : [];
            return View(LearningLessonViewModel.ForJourney(lesson, journey, review));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load a Lesson; rendering the Unavailable State instead.");
            return this.UnavailableView(_localizer["Learning.Title"], _localizer["Learning.Message.LessonUnavailable"],
                "Learning", nameof(Lesson), new Dictionary<string, string> { ["slug"] = slug });
        }
    }

    /// <summary>
    /// Finishes the Lesson once its Check is answered (AJAX from lesson-check.js), with the browser's time zone. 204 on
    /// success; 404 for an unknown slug and 409 for a locked Lesson, as ApiService answers.
    /// </summary>
    [Authorize]
    [HttpPost("/Learning/Lesson/{slug}/Finish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(string slug, [FromBody] FinishLessonRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _learningApi.FinishLessonAsync(slug, request ?? new FinishLessonRequest(),
                AppLanguages.Current().Code, cancellationToken);
            return NoContent();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            return StatusCode((int)ex.StatusCode);
        }
    }

    /// <summary>
    /// Shows or hides the quiet Atelier card on the Dashboard (#523): the card's Hide button and the Atelier page's
    /// switch post here, then go back where they came from. A failure leaves the preference as it was.
    /// </summary>
    [Authorize]
    [HttpPost("/Learning/Dashboard")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShowOnDashboard([FromForm] bool show, [FromForm] string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _learningApi.SetShowOnDashboardAsync(new ShowOnDashboardRequest { Show = show }, cancellationToken);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to save the Atelier Dashboard preference; it stays as it was.");
        }

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index), "Learning", null, "journey");
    }

    /// <summary>
    /// Marks a turned Review card "Knew it" or "Not yet" (AJAX from review-cards.js). 204 on success; 404 when the Term is
    /// not in a finished Lesson, as ApiService answers.
    /// </summary>
    [Authorize]
    [HttpPost("/Learning/Review/{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCard(string slug, [FromBody] MarkReviewCardRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _learningApi.MarkReviewCardAsync(slug, request ?? new MarkReviewCardRequest(), cancellationToken);
            return NoContent();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// The sitemap: <c>/Learning</c> and every Published Term Entry, in English (Language is a cookie). While the
    /// Library is empty, or ApiService cannot be read, it lists no entries.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken = default)
    {
        IEnumerable<string> termSlugs = [];
        try
        {
            LearningLibraryDto library = await _learningApi.GetLibraryAsync(AppLanguages.Default, cancellationToken);
            termSlugs = library.Topics.SelectMany(t => t.Terms).Select(t => t.Slug);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Library for the sitemap; listing no Term Entries.");
        }

        IEnumerable<string> urls = new[] { Url.Action(nameof(Index), "Learning", null, Request.Scheme)! }
            .Concat(termSlugs.Order(StringComparer.Ordinal).Select(TermUrl));
        var sitemap = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNamespace + "urlset",
                urls.Select(url => new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", url)))));
        return Content(sitemap.Declaration + Environment.NewLine + sitemap.Root, "application/xml", Encoding.UTF8);
    }

    /// <summary><c>robots.txt</c>: the public pages may be crawled, and the sitemap lists the Term Entries.</summary>
    [AllowAnonymous]
    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        string sitemapUrl = Url.Action(nameof(Sitemap), "Learning", null, Request.Scheme)!;
        return Content($"User-agent: *\nDisallow: /Identity/\nAllow: /\n\nSitemap: {sitemapUrl}\n", "text/plain",
            Encoding.UTF8);
    }

    /// <summary>Today's Review cards, or none when ApiService cannot be read: the Review never blocks the Lesson.</summary>
    private async Task<IReadOnlyList<ReviewCardDto>> ReviewOrNoneAsync(string language,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _learningApi.GetReviewAsync(language, cancellationToken)).Cards;
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to load the Review; opening the Lesson without it.");
            return [];
        }
    }

    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    private string TermUrl(string slug) => Url.Action(nameof(Term), "Learning", new { slug }, Request.Scheme)!;
}
