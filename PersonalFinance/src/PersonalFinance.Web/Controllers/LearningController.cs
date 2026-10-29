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
/// The Atelier's public pages (#513): the Library and each Term Entry, readable without signing in, plus the
/// <c>robots.txt</c> and <c>/sitemap.xml</c> that let a search engine find the entries. Content comes from ApiService
/// through <see cref="ILearningApi"/> in the request's Language.
/// </summary>
[AllowAnonymous]
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

    /// <summary>The Library: book spines on a shelf per Topic, or a "being written" note while it is empty.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        try
        {
            LearningLibraryDto library =
                await _learningApi.GetLibraryAsync(AppLanguages.Current().Code, cancellationToken);
            return View(new LearningIndexViewModel { Topics = library.Topics });
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
    /// The sitemap: <c>/Learning</c> and every Published Term Entry, in English (Language is a cookie). While the
    /// Library is empty, or ApiService cannot be read, it lists no entries.
    /// </summary>
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
    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        string sitemapUrl = Url.Action(nameof(Sitemap), "Learning", null, Request.Scheme)!;
        return Content($"User-agent: *\nDisallow: /Identity/\nAllow: /\n\nSitemap: {sitemapUrl}\n", "text/plain",
            Encoding.UTF8);
    }

    private string TermUrl(string slug) => Url.Action(nameof(Term), "Learning", new { slug }, Request.Scheme)!;
}
