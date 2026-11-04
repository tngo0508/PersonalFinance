namespace PersonalFinance.Shared.DTOs.News;

/// <summary>
///     What a news summary was built from, carried on <see cref="NewsArticleSummaryResponseDto.Coverage"/> so the
///     reader is told how much of the article the model actually saw.
/// </summary>
public static class NewsSummaryCoverage
{
    /// <summary>The article body: the feed carried it, or the article page could be read.</summary>
    public const string FullArticle = "FullArticle";

    /// <summary>Only the feed's short description: the page could not be read or had too little text.</summary>
    public const string FeedExcerpt = "FeedExcerpt";

    /// <summary>Only the headline: the feed carried no text and the link cannot be read (Google News redirects).</summary>
    public const string HeadlineOnly = "HeadlineOnly";
}
