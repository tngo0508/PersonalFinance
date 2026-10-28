using System.Text.RegularExpressions;

namespace PersonalFinance.Tests.Learning.ContentChecks;

/// <summary>How the content checks count words and sentences.</summary>
internal static partial class ContentText
{
    /// <summary>Words: runs of letters or digits, so "$1,000", "24%" and "don't" are one word each.</summary>
    public static int Words(string? text) => text is null ? 0 : Word().Count(text);

    /// <summary>Sentences: pieces ending at ".", "!" or "?" before a space or the end, with at least one word.</summary>
    public static IReadOnlyList<string> Sentences(string? text) =>
        text is null
            ? []
            : SentenceEnd().Split(text.Trim()).Where(s => Words(s) > 0).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’.,\-][\p{L}\p{N}]+)*")]
    private static partial Regex Word();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();
}
