using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Web.Models.Feedback;

/// <summary>
/// The page Feedback was sent from: what the "Send feedback" links pass as <c>from</c>, and what the Send feedback
/// form accepts. Always an app-relative path (with its query string when it fits) of at most
/// <see cref="FeedbackLimits.PageUrlMaxLength"/> characters.
/// </summary>
internal static class FeedbackSourcePage
{
    private static readonly PathString FeedbackPath = new("/Feedback");

    /// <summary>
    /// The source page for a "Send feedback" link on the page <paramref name="request"/> is for: its path and query
    /// string, or just the path when the query would make it too long. On the Send feedback page itself it is the
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
    /// Returns <paramref name="page"/> when it is an app-relative path no longer than
    /// <see cref="FeedbackLimits.PageUrlMaxLength"/> and not the Send feedback page; otherwise null.
    /// </summary>
    public static string? Accept(string? page)
    {
        if (!AppRelativePath.IsValid(page) || page!.Length > FeedbackLimits.PageUrlMaxLength)
        {
            return null;
        }

        int queryStart = page.IndexOf('?', StringComparison.Ordinal);
        var path = new PathString(queryStart < 0 ? page : page[..queryStart]);
        return path.StartsWithSegments(FeedbackPath, StringComparison.OrdinalIgnoreCase) ? null : page;
    }
}
