using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Web.Models.Feedback;

/// <summary>
/// The page Feedback was sent from: what the "Send feedback" links pass as <c>from</c>, and what the Send feedback
/// form accepts. Always an app-relative path of at most <see cref="FeedbackLimits.PageUrlMaxLength"/> characters,
/// with only the <see cref="ViewQueryKeys"/> of its query string when they fit (#124).
/// </summary>
internal static class FeedbackSourcePage
{
    private static readonly PathString FeedbackPath = new("/Feedback");

    /// <summary>
    /// The query keys kept on the source page: short view state that helps an Admin reproduce what the Account saw,
    /// never free text or record ids, which would be stored next to the Account and shown in the Admin inbox.
    /// </summary>
    private static readonly HashSet<string> ViewQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "month", "year", "status", "tab", "category", "source", "includeArchived", "ticker"
    };

    /// <summary>
    /// The source page for a "Send feedback" link on the page <paramref name="request"/> is for: its path and the
    /// view keys of its query string, or just the path when they would make it too long. On the Send feedback page itself it is the
    /// page that page was opened from, never the Send feedback page.
    /// </summary>
    public static string? From(HttpRequest request)
    {
        if (request.Path.StartsWithSegments(FeedbackPath, StringComparison.OrdinalIgnoreCase))
        {
            return Accept(request.Query["from"].ToString());
        }

        string? path = request.Path.Value;
        return Accept(path + request.QueryString.Value) ?? Accept(path);
    }

    /// <summary>
    /// Returns <paramref name="page"/>, keeping only the <see cref="ViewQueryKeys"/> of its query string, when it is
    /// an app-relative path no longer than <see cref="FeedbackLimits.PageUrlMaxLength"/> and not the Send feedback
    /// page; otherwise null.
    /// </summary>
    public static string? Accept(string? page)
    {
        if (!AppRelativePath.IsValid(page))
        {
            return null;
        }

        int queryStart = page!.IndexOf('?', StringComparison.Ordinal);
        string pathPart = queryStart < 0 ? page : page[..queryStart];
        if (new PathString(pathPart).StartsWithSegments(FeedbackPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string kept = queryStart < 0 ? pathPart : pathPart + ViewQuery(page[(queryStart + 1)..]);
        return kept.Length > FeedbackLimits.PageUrlMaxLength ? null : kept;
    }

    /// <summary>
    /// The pairs of <paramref name="query"/> (without its leading '?') whose key is a view key, in their original
    /// order and encoding, as a query string; empty when none is kept.
    /// </summary>
    private static string ViewQuery(string query)
    {
        List<string> pairs = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair =>
            {
                int equals = pair.IndexOf('=', StringComparison.Ordinal);
                string key = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]);
                return ViewQueryKeys.Contains(key);
            })
            .ToList();
        return pairs.Count == 0 ? string.Empty : "?" + string.Join('&', pairs);
    }
}
