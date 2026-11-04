namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// Reads the readable text of an article's web page, for a summary when the feed carried only an excerpt.
/// </summary>
internal interface IArticlePageClient
{
    /// <summary>
    /// The page's paragraph text, capped, or null when the page cannot be read (a block, a timeout, a paywall shell,
    /// not HTML) so the caller falls back to the feed's text.
    /// </summary>
    Task<string?> ReadTextAsync(string url, CancellationToken cancellationToken);
}
