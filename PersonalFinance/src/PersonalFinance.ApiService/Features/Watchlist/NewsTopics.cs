namespace PersonalFinance.ApiService.Features.Watchlist;

/// <summary>
/// The topics a finance news source is filed under. The feeds send no category of their own, so an article's topic is
/// its source's, and the News page offers as filters only the topics some configured source covers.
/// </summary>
internal static class NewsTopics
{
    public const string Budgeting = "Budgeting";
    public const string Investing = "Investing";
    public const string Retirement = "Retirement";
    public const string General = "General";
}
