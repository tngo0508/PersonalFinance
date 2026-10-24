using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads a Form N-PORT primary document (<c>primary_doc.xml</c>, namespace <c>http://www.sec.gov/edgar/nport</c>)
/// as a stream: a world-stock fund's report lists some 10,000 holdings in about 9 MB, so the document is never
/// loaded whole. Elements are matched by local name, so a change of namespace prefix does not matter.
/// </summary>
internal static class NportDocumentParser
{
    private const string OtherCategory = "OTHER";

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    /// <summary>
    /// Parses the report. Throws <see cref="XmlException"/> on a malformed document, and
    /// <see cref="FormatException"/> when it has no readable report date.
    /// </summary>
    public static async Task<NportReport> ParseAsync(Stream document, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(document, ReaderSettings);
        string? seriesId = null;
        string? reportDate = null;
        decimal netAssets = 0m;
        var holdings = new List<NportHolding>();

        bool more = await reader.ReadAsync();
        while (more)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                more = await reader.ReadAsync();
                continue;
            }

            // Each branch reads the whole element, which leaves the reader on the next node already.
            switch (reader.LocalName)
            {
                case "seriesId" when seriesId is null:
                    seriesId = (await reader.ReadElementContentAsStringAsync()).Trim();
                    break;
                case "repPdDate":
                    reportDate = (await reader.ReadElementContentAsStringAsync()).Trim();
                    break;
                case "netAssets":
                    netAssets = ParseDecimal(await reader.ReadElementContentAsStringAsync());
                    break;
                case "invstOrSec":
                    var element = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                    holdings.Add(ReadHolding(element));
                    break;
                default:
                    more = await reader.ReadAsync();
                    continue;
            }

            more = !reader.EOF;
        }

        if (!DateOnly.TryParseExact(reportDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateOnly date))
        {
            throw new FormatException("The N-PORT document has no readable report date (repPdDate).");
        }

        return new NportReport(string.IsNullOrEmpty(seriesId) ? null : seriesId, date, netAssets, holdings);
    }

    /// <summary>
    /// One holding from its direct child elements: the issuer name (or the title when the name is missing or
    /// <c>N/A</c>), the % of net assets, and the asset category from <c>assetCat</c> or, for a category N-PORT has no
    /// code for, <c>assetConditional/@assetCat</c>.
    /// </summary>
    private static NportHolding ReadHolding(XElement element)
    {
        string? Child(string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

        string? name = Child("name");
        if (string.IsNullOrEmpty(name) || name == "N/A")
        {
            name = Child("title");
        }

        string? category = Child("assetCat");
        if (string.IsNullOrEmpty(category))
        {
            category = element.Elements().FirstOrDefault(e => e.Name.LocalName == "assetConditional")
                ?.Attribute("assetCat")?.Value.Trim();
        }

        return new NportHolding(string.IsNullOrEmpty(name) ? "Unnamed holding" : name,
            string.IsNullOrEmpty(category) ? OtherCategory : category, ParseDecimal(Child("pctVal")));
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;
}
