using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads EDGAR's company browse feed (<c>cgi-bin/browse-edgar?action=getcompany&amp;...&amp;output=atom</c>): one
/// <c>entry</c> per filing, newest first, each with its accession number, filing date, form type and the link to its
/// index page, from which the filing's <c>primary_doc.xml</c> is found.
/// </summary>
internal static class EdgarFilingListParser
{
    private const string ArchivesPrefix = "https://www.sec.gov/Archives/edgar/data/";
    private const string PrimaryDocument = "primary_doc.xml";

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    /// <summary>
    /// The feed's filings, in feed order. An entry without a readable date, or whose link is not an EDGAR archive
    /// page, is left out. Throws <see cref="XmlException"/> on a malformed feed.
    /// </summary>
    public static async Task<List<NportFilingRef>> ParseAsync(Stream feed, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(feed, ReaderSettings);
        XDocument document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        var filings = new List<NportFilingRef>();
        foreach (XElement content in document.Descendants().Where(e => e.Name.LocalName == "content"))
        {
            string? Child(string name) =>
                content.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

            string? accession = Child("accession-number");
            string? href = Child("filing-href");
            if (string.IsNullOrEmpty(accession)
                || href is null
                || !href.StartsWith(ArchivesPrefix, StringComparison.Ordinal)
                || !DateOnly.TryParseExact(Child("filing-date"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly filedOn))
            {
                continue;
            }

            string folder = href[..(href.LastIndexOf('/') + 1)];
            filings.Add(new NportFilingRef(accession, filedOn, Child("filing-type") ?? string.Empty,
                folder + PrimaryDocument));
        }

        return filings;
    }
}
