namespace PersonalFinance.Web.Models.Community;

/// <summary>
/// The reply thread under a Strategy or a poll, for the <c>_Thread</c> partial: what the replies are posted to, and the
/// shown Comments, oldest first. Exactly one of <see cref="StrategyId"/> and <see cref="PollCode"/> is set.
/// </summary>
/// <param name="StrategyId">The Strategy replied to, if any.</param>
/// <param name="PollCode">The poll replied to, by its code, if any.</param>
/// <param name="Comments">The thread's Comments.</param>
public sealed record CommunityThread(int? StrategyId, string? PollCode, IReadOnlyList<CommunityComment> Comments)
{
    /// <summary>A key unique on the page, for element ids.</summary>
    public string Key => StrategyId is int id ? $"s{id}" : $"p{PollCode}";
}
