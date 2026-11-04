using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// Turns feed HTML and article pages into plain text for the News page and the AI summary.
/// </summary>
internal static partial class HtmlText
{
    /// <summary>Plain text of an HTML fragment: block ends become line breaks, tags go, entities are decoded.</summary>
    public static string Strip(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        string text = BlockEnds().Replace(html, "\n");
        text = Tags().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        text = Spaces().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// The paragraphs of an article page, each at least <paramref name="minParagraphLength"/> characters so menus and
    /// captions drop out, after scripts, styles and the page chrome are removed. Empty when the page has none.
    /// </summary>
    public static string Paragraphs(string html, int minParagraphLength)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        string body = Chrome().Replace(html, string.Empty);
        var text = new StringBuilder();
        foreach (Match paragraph in ParagraphElements().Matches(body))
        {
            string line = Strip(paragraph.Groups["text"].Value);
            if (line.Length < minParagraphLength)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append("\n\n");
            }

            text.Append(line);
        }

        return text.ToString();
    }

    /// <summary>The first <paramref name="maxLength"/> characters, with no marker added.</summary>
    public static string Cap(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    [GeneratedRegex(@"<(script|style|noscript|nav|header|footer|aside|svg)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Chrome();

    [GeneratedRegex(@"<p\b[^>]*>(?<text>.*?)</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphElements();

    [GeneratedRegex(@"</(p|div|li|h[1-6]|blockquote|tr)\s*>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnds();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex("[ \t\r\f\v]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s*\n(\s*\n)+\s*")]
    private static partial Regex BlankLines();
}
