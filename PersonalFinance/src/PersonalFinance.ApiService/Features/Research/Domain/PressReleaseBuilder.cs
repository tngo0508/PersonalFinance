using System.Net;
using System.Text.RegularExpressions;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The rules of a Company Profile's press releases: the announcements a company attached to its 8-K filings as
/// exhibit 99, never third-party coverage.
/// <list type="bullet">
/// <item>Filings: the company's 8-K and 8-K/A filings of the last 12 months, newest first
/// (<see cref="Filings"/>); the reader stops once it has <see cref="MaxReleases"/> releases.</item>
/// <item>Documents: every exhibit typed <c>EX-99</c>, <c>EX-99.1</c>, <c>EX-99.2</c> and so on, in the order the
/// filing lists them; only HTML and text documents are read (<see cref="IsReadable"/>).</item>
/// <item>Headline: the first line of <see cref="MinHeadlineLength"/> to <see cref="MaxHeadlineLength"/> characters of
/// the document's text that is not just the exhibit number or the file name (<see cref="HeadlineOf"/>); else the
/// 8-K's events in plain words, or its form when it reports none.</item>
/// </list>
/// </summary>
internal static class PressReleaseBuilder
{
    /// <summary>The most releases listed.</summary>
    public const int MaxReleases = 10;

    /// <summary>How many months back filings are read.</summary>
    private const int MonthsBack = 12;

    /// <summary>The shortest line taken as a headline.</summary>
    private const int MinHeadlineLength = 20;

    /// <summary>The longest line taken as a headline.</summary>
    private const int MaxHeadlineLength = 160;

    /// <summary>What separates the event words of a fallback headline.</summary>
    private const string EventSeparator = " · ";

    /// <summary>The forms whose exhibits are press releases.</summary>
    private static readonly HashSet<string> Forms = new HashSet<string>(StringComparer.Ordinal) { "8-K", "8-K/A" };

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly string[] ReadableExtensions = [".htm", ".html", ".txt"];

    private static readonly Regex Comments = new(@"<!--.*?(?:-->|$)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex ScriptsAndStyles = new(@"<(script|style)\b.*?(?:</\1\s*>|$)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    // Tags that start a new line where a browser would, and the fields of the SGML document header that some exhibits
    // on sec.gov start with (<DOCUMENT><TYPE>EX-99.1<SEQUENCE>2<FILENAME>...<TEXT>), one value per line; any other tag
    // is dropped and its text runs on.
    private static readonly Regex BlockTags = new(
        @"</?(?:p|div|br|hr|tr|td|th|li|ul|ol|dt|dd|h[1-6]|title|table|caption|center|section|article|header|footer|blockquote|pre|body|head|html|document|type|sequence|filename|description|text)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    // A tag, or a tag cut off by the end of the document's head.
    private static readonly Regex Tags = new(@"<[^>]*(?:>|$)", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex ExhibitNumber = new(@"^(?:ex|exhibit)99\d*$",
        RegexOptions.CultureInvariant, MatchTimeout);

    // The exhibit label anywhere in a line: "EX-99.1", "Exhibit 99.1", "Exhibit 99".
    private static readonly Regex ExhibitLabel = new(@"\b(?:ex|exhibit)[\s\-]*99(?:\.\d+)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>
    /// The filings whose exhibits may be press releases: 8-K and 8-K/A filings of the <see cref="MonthsBack"/> months
    /// up to <paramref name="today"/>, newest first.
    /// </summary>
    public static IReadOnlyList<SecFilingEntry> Filings(SecCompanySubmissions submissions, DateOnly today)
    {
        DateOnly from = today.AddMonths(-MonthsBack);
        return submissions.RecentFilings
            .Where(f => Forms.Contains(f.Form) && f.FiledOn >= from && f.FiledOn <= today)
            .OrderByDescending(f => f.FiledOn)
            .ThenByDescending(f => f.AccessionNumber, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// True when <paramref name="fileName"/> is an HTML or text document whose headline can be read; a PDF or an image
    /// is listed with the fallback headline instead.
    /// </summary>
    public static bool IsReadable(string fileName) =>
        ReadableExtensions.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One release: exhibit <paramref name="fileName"/> of <paramref name="filing"/>, with its headline read from
    /// <paramref name="head"/> (the start of the document, or null when it was not read).
    /// </summary>
    public static CompanyPressReleaseDto BuildRelease(long cik, SecFilingEntry filing, string fileName, string? head)
    {
        List<string> events = filing.Items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(EightKItems.Describe)
            .ToList();
        string? headline = head is null ? null : HeadlineOf(head, fileName);
        return new CompanyPressReleaseDto
        {
            Headline = headline ?? (events.Count > 0 ? string.Join(EventSeparator, events) : filing.Form),
            FiledOn = filing.FiledOn,
            Events = events,
            Url = EdgarArchive.FileUrl(cik, filing.AccessionNumber, fileName),
            HeadlineRead = headline is not null,
        };
    }

    /// <summary>
    /// The headline of a release: with tags, scripts, styles and comments stripped and whitespace collapsed, the first
    /// line of <see cref="MinHeadlineLength"/> to <see cref="MaxHeadlineLength"/> characters that is not just
    /// <c>EX-99.x</c>, <c>Exhibit 99.x</c> or <paramref name="fileName"/> (ignoring case, punctuation and spacing).
    /// Null when there is none.
    /// </summary>
    private static string? HeadlineOf(string head, string fileName)
    {
        string fileKey = Key(fileName);
        return TextLines(head).FirstOrDefault(line =>
            line.Length is >= MinHeadlineLength and <= MaxHeadlineLength
            && !ExhibitNumber.IsMatch(Key(line))
            && Key(line) != fileKey);
    }

    /// <summary>
    /// The document's text, one collapsed, non-empty line per block. The document's own line breaks are whitespace,
    /// as a browser treats them, so a headline wrapped across source lines stays one line; only block tags break it.
    /// In a block that also carries the exhibit label (<c>Exhibit 99.1</c>), the source lines holding the label are
    /// dropped first, so a slide title is not run together with its "September 2026 Exhibit 99.1" footer.
    /// </summary>
    private static IEnumerable<string> TextLines(string head)
    {
        string text = Comments.Replace(head, string.Empty);
        text = ScriptsAndStyles.Replace(text, string.Empty);
        foreach (string block in BlockTags.Split(text))
        {
            string[] sourceLines = WebUtility.HtmlDecode(Tags.Replace(block, string.Empty)).Split('\n');
            IEnumerable<string> kept = sourceLines.Any(ExhibitLabel.IsMatch)
                ? sourceLines.Where(line => !ExhibitLabel.IsMatch(line))
                : sourceLines;
            string line = Whitespace.Replace(string.Join(' ', kept), " ").Trim();
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    /// <summary>Letters and digits only, in lower case: <c>Exhibit 99.1</c> is <c>exhibit991</c>.</summary>
    private static string Key(string text) =>
        string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
