using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using PersonalFinance.Shared.DTOs.Learning;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Links the Terms a Term Entry's text names to their own entries (#513, "terms mentioned in the text"). Each mention
/// matches as whole words, ignoring case, and the longest wins where two overlap ("capital gains tax" over "tax").
/// Every other character is HTML-encoded, so content text can never inject markup.
/// </summary>
public static class TermTextLinker
{
    /// <summary>
    /// <paramref name="text"/> as HTML, each mention linked to <paramref name="termUrl"/>(slug). An entry is linked
    /// once per text, at its first mention.
    /// </summary>
    public static IHtmlContent Link(string text, IReadOnlyList<TermMentionDto> mentions, Func<string, string> termUrl)
    {
        HtmlEncoder encoder = HtmlEncoder.Default;
        if (string.IsNullOrEmpty(text) || mentions.Count == 0)
        {
            return new HtmlString(encoder.Encode(text ?? string.Empty));
        }

        // Longest first, so the alternation prefers the longer of two overlapping mentions.
        IEnumerable<TermMentionDto> ordered = mentions
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .OrderByDescending(m => m.Text.Length);
        var pattern = new Regex(
            @"(?<![\p{L}\p{N}])(?:" + string.Join("|", ordered.Select(m => Regex.Escape(m.Text))) + @")(?![\p{L}\p{N}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        var linked = new HashSet<string>(StringComparer.Ordinal);
        var html = new StringBuilder();
        int last = 0;
        foreach (Match match in pattern.Matches(text))
        {
            TermMentionDto? mention = mentions.FirstOrDefault(m =>
                string.Equals(m.Text, match.Value, StringComparison.OrdinalIgnoreCase));
            if (mention is null || !linked.Add(mention.Slug))
            {
                continue;
            }

            html.Append(encoder.Encode(text[last..match.Index]))
                .Append("<a class=\"atelier-term-link\" href=\"").Append(encoder.Encode(termUrl(mention.Slug))).Append("\">")
                .Append(encoder.Encode(match.Value))
                .Append("</a>");
            last = match.Index + match.Length;
        }

        html.Append(encoder.Encode(text[last..]));
        return new HtmlString(html.ToString());
    }
}
