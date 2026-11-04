using PersonalFinance.Shared.DTOs.Watchlist;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// One feed item as the feed is cached: the article the page shows, and the longest text the feed carried for it
/// (<c>content:encoded</c> when present, else the description), HTML stripped and capped, kept server-side for the
/// AI summary and never sent to the page.
/// </summary>
internal sealed class NewsFeedItem
{
    public required NewsArticleDto Article { get; init; }

    public string FullText { get; init; } = string.Empty;
}
