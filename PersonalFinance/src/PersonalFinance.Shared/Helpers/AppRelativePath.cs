namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Recognises app-relative paths such as <c>/Dashboard</c>, the only page form Feedback stores.
/// </summary>
public static class AppRelativePath
{
    /// <summary>
    /// True for a path that starts with a single <c>/</c>; false for absolute URLs and the protocol-relative forms
    /// (<c>//host</c>, <c>/\host</c>) that a browser would resolve to another site.
    /// </summary>
    public static bool IsValid(string? path)
    {
        return !string.IsNullOrEmpty(path)
               && path.StartsWith('/')
               && !path.StartsWith("//", StringComparison.Ordinal)
               && !path.Contains('\\');
    }
}
