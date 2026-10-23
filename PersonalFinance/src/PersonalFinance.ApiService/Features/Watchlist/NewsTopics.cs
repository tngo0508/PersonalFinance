namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// The topics a finance news source is filed under. An article's topic is always its source's: categories a feed puts
/// on its own items (NerdWallet's "Mortgages", say) are ignored, so the topic filter matches every article. The News
/// page offers as filters only the topics some configured source covers.
/// </summary>
internal static class NewsTopics
{
    public const string Budgeting = "Budgeting";
    public const string Investing = "Investing";
    public const string Retirement = "Retirement";
    public const string General = "General";
}
