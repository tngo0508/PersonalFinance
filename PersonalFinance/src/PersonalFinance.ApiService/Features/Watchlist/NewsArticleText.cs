using PersonalFinance.Shared.DTOs.News;

namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// The text of one article of the news feed that a summary is written from, with the
/// <see cref="NewsSummaryCoverage"/> saying whether it is the article body, the feed's excerpt or the headline alone.
/// </summary>
public sealed record NewsArticleText(string Title, string Text, string Coverage);
