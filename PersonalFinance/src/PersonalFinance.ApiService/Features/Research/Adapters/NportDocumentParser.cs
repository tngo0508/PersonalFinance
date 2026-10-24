using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PersonalFinance.ApiService.Features.Research.Domain;

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
    /// <c>N/A</c>), the % of net assets, the asset category from <c>assetCat</c> or, for a category N-PORT has no
    /// code for, <c>assetConditional/@assetCat</c>, the security key (ISIN, else CUSIP; held in memory only), and
    /// whether it is securities-lending cash collateral.
    /// </summary>
    private static NportHolding ReadHolding(XElement element)
    {
        string? Child(string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();
        XElement? Descendant(string name) => element.Descendants().FirstOrDefault(e => e.Name.LocalName == name);

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

        string? isin = Descendant("isin")?.Attribute("value")?.Value.Trim();
        string? key = IsIdentifier(isin, 12) ? isin!.ToUpperInvariant() : null;
        if (key is null)
        {
            string? cusip = Child("cusip");
            key = IsIdentifier(cusip, 9) ? cusip!.ToUpperInvariant() : null;
        }
        bool collateral = string.Equals(Descendant("isCashCollateral")?.Value.Trim(), "Y", StringComparison.OrdinalIgnoreCase);

        return new NportHolding(string.IsNullOrEmpty(name) ? "Unnamed holding" : name,
            string.IsNullOrEmpty(category) ? OtherCategory : category, ParseDecimal(Child("pctVal")), key, collateral);
    }

    /// <summary>
    /// Whether a CUSIP or ISIN as reported is a real identifier of its length: not missing, not <c>N/A</c>, and not a
    /// run of zeros (which funds use for "none").
    /// </summary>
    private static bool IsIdentifier(string? value, int length) =>
        value is not null && value.Length == length && value.All(char.IsLetterOrDigit) && value.Any(c => c is not '0');

    /// <summary>
    /// Parses only the head of the report: its series, report date and each class's monthly total returns
    /// (<c>returnInfo/monthlyTotReturns/monthlyTotReturn</c>, attributes <c>classId</c>, <c>rtn1</c> to <c>rtn3</c>).
    /// It stops at the holdings, so the rest of the document is never read. Throws <see cref="XmlException"/> on a
    /// malformed head, and <see cref="FormatException"/> when it has no readable report date.
    /// </summary>
    public static async Task<NportReturns> ParseReturnsAsync(Stream document, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(document, ReaderSettings);
        string? seriesId = null;
        string? reportDate = null;
        var classes = new List<NportClassReturns>();

        bool more = await reader.ReadAsync();
        while (more)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "invstOrSecs":
                        more = false;
                        continue;
                    case "seriesId" when seriesId is null:
                        seriesId = (await reader.ReadElementContentAsStringAsync()).Trim();
                        more = !reader.EOF;
                        continue;
                    case "repPdDate":
                        reportDate = (await reader.ReadElementContentAsStringAsync()).Trim();
                        more = !reader.EOF;
                        continue;
                    case "monthlyTotReturn":
                        classes.Add(new NportClassReturns(BlankToNull(reader.GetAttribute("classId")),
                            ParseReturn(reader.GetAttribute("rtn1")), ParseReturn(reader.GetAttribute("rtn2")),
                            ParseReturn(reader.GetAttribute("rtn3"))));
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "returnInfo")
            {
                break;
            }

            more = await reader.ReadAsync();
        }

        if (!DateOnly.TryParseExact(reportDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateOnly date))
        {
            throw new FormatException("The N-PORT document has no readable report date (repPdDate).");
        }

        return new NportReturns(string.IsNullOrEmpty(seriesId) ? null : seriesId, date, classes);
    }

    /// <summary>A monthly return as reported, in percent; null when it is missing or not a number (e.g. N/A).</summary>
    private static decimal? ParseReturn(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;
}
