using System.Net;
using System.Text.RegularExpressions;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads a filing's index headers page (<c>{accession}-index-headers.html</c>): the filing's SGML headers, HTML-escaped
/// inside a <c>&lt;PRE&gt;</c>, with one <c>&lt;DOCUMENT&gt;</c> block per document giving its <c>&lt;TYPE&gt;</c> and
/// <c>&lt;FILENAME&gt;</c>.
/// </summary>
internal static class FilingIndexHeadersParser
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex DocumentBlock = new(@"<DOCUMENT>(.*?)</DOCUMENT>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex TypeTag = new(@"<TYPE>[ \t]*([^\s<]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex FileNameTag = new(@"<FILENAME>[ \t]*([^\s<]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    // A plain file name in the filing's folder; anything else (a path, a query) is never requested.
    private static readonly Regex SafeFileName = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$",
        RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>
    /// The file names of the documents typed <c>EX-99</c>, <c>EX-99.1</c>, <c>EX-99.2</c> and so on, in the order the
    /// filing lists them. A block without a type or a plain file name is skipped.
    /// </summary>
    public static IReadOnlyList<string> PressReleaseFileNames(string indexHeaders)
    {
        string text = WebUtility.HtmlDecode(indexHeaders);
        var files = new List<string>();
        foreach (Match block in DocumentBlock.Matches(text))
        {
            string body = block.Groups[1].Value;
            Match type = TypeTag.Match(body);
            Match file = FileNameTag.Match(body);
            if (type.Success && file.Success
                             && type.Groups[1].Value.StartsWith("EX-99", StringComparison.OrdinalIgnoreCase)
                             && SafeFileName.IsMatch(file.Groups[1].Value))
            {
                files.Add(file.Groups[1].Value);
            }
        }

        return files;
    }
}
