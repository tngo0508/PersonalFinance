using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="ISecEdgarClient"/> and <see cref="ISecCompanyClient"/> over HTTP, registered as a typed
/// <see cref="HttpClient"/>. Every request carries the configured User-Agent and first waits its turn at the shared
/// <see cref="SecRequestGate"/>.
/// </summary>
internal sealed class SecEdgarClient : ISecEdgarClient, ISecCompanyClient
{
    private const string BaseUrl = "https://data.sec.gov/";

    // The fund ticker list and EDGAR's filing lists are served from www.sec.gov (filing archives: EdgarArchive).
    private const string WwwUrl = "https://www.sec.gov/";

    // The most filings EDGAR's company browse feed lists on one page.
    private const int FilingListPageSize = 100;

    /// <summary>How much of a document <see cref="GetDocumentHeadAsync"/> reads: its first 64 KB.</summary>
    public const int DocumentHeadBytes = 64 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly HttpClient _httpClient;
    private readonly SecEdgarSettings _settings;
    private readonly SecRequestGate _gate;

    public SecEdgarClient(HttpClient httpClient, SecEdgarSettings settings, SecRequestGate gate)
    {
        _httpClient = httpClient;
        _settings = settings;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublicFloatFact>> GetPublicFloatFrameAsync(int year, int quarter,
        CancellationToken cancellationToken)
    {
        List<(FrameRow Row, DateOnly End, decimal Value)> rows =
            await GetFrameAsync("dei/EntityPublicFloat", year, quarter, cancellationToken);
        return rows.Select(r => new PublicFloatFact(r.Row.Cik, r.Row.EntityName ?? string.Empty, r.End, r.Value))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TotalAssetsFact>> GetTotalAssetsFrameAsync(int year, int quarter,
        CancellationToken cancellationToken)
    {
        List<(FrameRow Row, DateOnly End, decimal Value)> rows =
            await GetFrameAsync("us-gaap/Assets", year, quarter, cancellationToken);
        return rows.Select(r => new TotalAssetsFact(r.Row.Cik, r.End, r.Value)).ToList();
    }

    /// <summary>
    /// Reads one US$ instant frame (<c>CY{year}Q{quarter}I</c>) of a concept, keeping the rows with a value and a
    /// readable date. A frame SEC does not have (404) is empty.
    /// </summary>
    private async Task<List<(FrameRow Row, DateOnly End, decimal Value)>> GetFrameAsync(string concept, int year,
        int quarter, CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}api/xbrl/frames/{concept}/USD/CY{year}Q{quarter}I.json");

        await _gate.WaitAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _settings.UserAgent);
        using HttpResponseMessage response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        FrameResponse? frame = await JsonSerializer.DeserializeAsync<FrameResponse>(body, JsonSerializerOptions.Web,
            cancellationToken);

        var rows = new List<(FrameRow Row, DateOnly End, decimal Value)>();
        foreach (FrameRow row in frame?.Data ?? [])
        {
            if (row.Val is { } value
                && DateOnly.TryParseExact(row.End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateOnly end))
            {
                rows.Add((row, end, value));
            }
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<SecFiler?> GetFilerAsync(long cik, CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}submissions/CIK{cik:0000000000}.json");

        await _gate.WaitAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _settings.UserAgent);
        using HttpResponseMessage response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        SubmissionsResponse? submissions = await JsonSerializer.DeserializeAsync<SubmissionsResponse>(body,
            JsonSerializerOptions.Web, cancellationToken);
        return submissions is null
            ? null
            : new SecFiler(cik, submissions.Name ?? string.Empty, BlankToNull(submissions.Tickers?.FirstOrDefault()),
                BlankToNull(submissions.Sic), BlankToNull(submissions.SicDescription),
                BlankToNull(submissions.OwnerOrg));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecFundSeries>> GetFundSeriesAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await SendAsync($"{WwwUrl}files/company_tickers_mf.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument list = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

        // { "fields": ["cik", "seriesId", "classId", "symbol"], "data": [[2110, "S000009184", "C000024954", "LACAX"], ...] }
        var series = new List<SecFundSeries>();
        foreach (JsonElement row in list.RootElement.GetProperty("data").EnumerateArray())
        {
            if (row.GetArrayLength() >= 4
                && row[0].TryGetInt64(out long cik)
                && row[1].GetString() is { Length: > 0 } seriesId
                && row[3].GetString() is { Length: > 0 } ticker)
            {
                string? classId = row[2].ValueKind == JsonValueKind.String && row[2].GetString() is { Length: > 0 } id
                    ? id
                    : null;
                series.Add(new SecFundSeries(cik, seriesId, ticker, classId));
            }
        }

        return series;
    }

    /// <inheritdoc />
    // EDGAR's type filter is a prefix match, so NPORT-P also lists the NPORT-P/A amendments.
    public Task<IReadOnlyList<NportFilingRef>> GetNportFilingsAsync(string filer, DateOnly filedFrom,
        CancellationToken cancellationToken) =>
        GetFilingListAsync(filer, "NPORT-P", filedFrom, cancellationToken);

    /// <inheritdoc />
    // N-CSR also lists the semi-annual N-CSRS and the amendments.
    public Task<IReadOnlyList<NportFilingRef>> GetShareholderReportFilingsAsync(string filer, DateOnly filedFrom,
        CancellationToken cancellationToken) =>
        GetFilingListAsync(filer, "N-CSR", filedFrom, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassExpenseRatio>?> GetExpenseRatiosAsync(NportFilingRef filing,
        CancellationToken cancellationToken)
    {
        string folder = filing.DocumentUrl[..(filing.DocumentUrl.LastIndexOf('/') + 1)];
        string? instance;
        using (HttpResponseMessage index = await SendAsync($"{folder}index.json", cancellationToken))
        {
            if (index.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            index.EnsureSuccessStatusCode();
            await using Stream body = await index.Content.ReadAsStreamAsync(cancellationToken);
            instance = await XbrlInstanceOfAsync(body, cancellationToken);
        }

        if (instance is null)
        {
            return null;
        }

        using HttpResponseMessage response = await SendAsync(folder + instance, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream xbrl = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await XbrlExpenseRatioParser.ParseAsync(xbrl, cancellationToken);
    }

    /// <summary>
    /// The XBRL instance a filing folder's <c>index.json</c> lists: the file EDGAR extracts from the inline XBRL report,
    /// named <c>*_htm.xml</c>; null when there is none (a filing without XBRL, such as SPY's).
    /// </summary>
    internal static async Task<string?> XbrlInstanceOfAsync(Stream indexJson, CancellationToken cancellationToken)
    {
        using JsonDocument index = await JsonDocument.ParseAsync(indexJson, cancellationToken: cancellationToken);
        if (!index.RootElement.TryGetProperty("directory", out JsonElement directory)
            || !directory.TryGetProperty("item", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return items.EnumerateArray()
            .Select(i => i.TryGetProperty("name", out JsonElement name) ? name.GetString() : null)
            .FirstOrDefault(n => n is not null && n.EndsWith("_htm.xml", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The filings of a form type listed under a CIK or series id, filed on or after a date.</summary>
    private async Task<IReadOnlyList<NportFilingRef>> GetFilingListAsync(string filer, string formType, DateOnly filedFrom,
        CancellationToken cancellationToken)
    {
        var filings = new List<NportFilingRef>();
        for (int start = 0;; start += FilingListPageSize)
        {
            string url = string.Create(CultureInfo.InvariantCulture,
                $"{WwwUrl}cgi-bin/browse-edgar?action=getcompany&CIK={Uri.EscapeDataString(filer)}&type={Uri.EscapeDataString(formType)}&dateb=&owner=include&start={start}&count={FilingListPageSize}&output=atom");
            using HttpResponseMessage response = await SendAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return filings;
            }

            response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            List<NportFilingRef> page = await EdgarFilingListParser.ParseAsync(body, cancellationToken);
            filings.AddRange(page.Where(f => f.FiledOn >= filedFrom));
            if (page.Count < FilingListPageSize || page.Exists(f => f.FiledOn < filedFrom))
            {
                return filings;
            }
        }
    }

    /// <inheritdoc />
    public async Task<NportReport?> GetNportReportAsync(NportFilingRef filing, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(filing.DocumentUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await NportDocumentParser.ParseAsync(body, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NportReturns?> GetNportReturnsAsync(NportFilingRef filing, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(filing.DocumentUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        // Only the head is read: disposing the response stops the download before the holdings.
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await NportDocumentParser.ParseReturnsAsync(body, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecCompanyListing>> GetCompanyTickersAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await SendAsync($"{WwwUrl}files/company_tickers_exchange.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument list = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

        // { "fields": ["cik", "name", "ticker", "exchange"], "data": [[320193, "Apple Inc.", "AAPL", "Nasdaq"], ...] }
        var listings = new List<SecCompanyListing>();
        foreach (JsonElement row in list.RootElement.GetProperty("data").EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Array
                && row.GetArrayLength() >= 4
                && row[0].ValueKind == JsonValueKind.Number
                && row[0].TryGetInt64(out long cik)
                && row[1].ValueKind == JsonValueKind.String
                && row[2].ValueKind == JsonValueKind.String
                && row[2].GetString() is { Length: > 0 } ticker)
            {
                string? exchange = row[3].ValueKind == JsonValueKind.String ? BlankToNull(row[3].GetString()) : null;
                listings.Add(new SecCompanyListing(cik, ticker, row[1].GetString() ?? string.Empty, exchange));
            }
        }

        return listings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetFundTickersAsync(CancellationToken cancellationToken) =>
        (await GetFundSeriesAsync(cancellationToken)).Select(s => s.Ticker).ToList();

    /// <inheritdoc />
    public async Task<SecCompanySubmissions?> GetCompanySubmissionsAsync(long cik, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}submissions/CIK{cik:0000000000}.json"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        CompanySubmissionsResponse? submissions = await JsonSerializer.DeserializeAsync<CompanySubmissionsResponse>(
            body, JsonSerializerOptions.Web, cancellationToken);
        return submissions is null ? null : ToSubmissions(cik, submissions);
    }

    /// <inheritdoc />
    public async Task<SecCompanyFacts?> GetCompanyFactsAsync(long cik, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}api/xbrl/companyfacts/CIK{cik:0000000000}.json"),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return ParseCompanyFacts(document.RootElement);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPressReleaseFileNamesAsync(long cik, string accessionNumber,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            EdgarArchive.FileUrl(cik, accessionNumber, $"{accessionNumber}-index-headers.html"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        string indexHeaders = await response.Content.ReadAsStringAsync(cancellationToken);
        return FilingIndexHeadersParser.PressReleaseFileNames(indexHeaders);
    }

    /// <inheritdoc />
    public async Task<string?> GetDocumentHeadAsync(long cik, string accessionNumber, string fileName,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await SendAsync(EdgarArchive.FileUrl(cik, accessionNumber, fileName), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        // A release can run to a megabyte; only its head is read, and the rest is never downloaded.
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] head = new byte[DocumentHeadBytes];
        int length = 0;
        int read;
        while (length < head.Length
               && (read = await body.ReadAsync(head.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        return DecodeHead(head, length);
    }

    /// <summary>
    /// The text of a document's head: UTF-8 when it is valid UTF-8 (allowing a character cut off at the end of the
    /// head), else Latin-1, which older filings use.
    /// </summary>
    private static string DecodeHead(byte[] head, int length)
    {
        int maxTrim = length == head.Length ? 3 : 0;
        for (int trim = 0; trim <= maxTrim && trim < length; trim++)
        {
            try
            {
                return StrictUtf8.GetString(head, 0, length - trim);
            }
            catch (DecoderFallbackException)
            {
                // Try again without the last byte: a character may have been cut off by the head limit.
            }
        }

        return Encoding.Latin1.GetString(head, 0, length);
    }

    /// <summary>
    /// The concepts of <see cref="CompanyProfileConcepts"/> from a <c>companyfacts</c> document:
    /// <c>{ "facts": { "us-gaap": { "Revenues": { "units": { "USD": [ { "start", "end", "val", "fy", "fp", "form",
    /// "filed", "accn" } ] } } } } }</c>. A row without a readable value or date is skipped.
    /// </summary>
    internal static SecCompanyFacts ParseCompanyFacts(JsonElement root)
    {
        var facts = new Dictionary<string, IReadOnlyList<SecFact>>(StringComparer.Ordinal);
        bool hasIfrs = false;
        if (TryGetObjectProperty(root, "facts", out JsonElement taxonomies))
        {
            hasIfrs = TryGetObjectProperty(taxonomies, "ifrs-full", out JsonElement ifrs)
                      && ifrs.ValueKind == JsonValueKind.Object
                      && ifrs.EnumerateObject().Any();
            foreach (CompanyConcept concept in CompanyProfileConcepts.All)
            {
                if (TryGetObjectProperty(taxonomies, concept.Taxonomy, out JsonElement taxonomy)
                    && TryGetObjectProperty(taxonomy, concept.Name, out JsonElement element)
                    && TryGetObjectProperty(element, "units", out JsonElement units)
                    && TryGetObjectProperty(units, concept.Unit, out JsonElement rows)
                    && rows.ValueKind == JsonValueKind.Array)
                {
                    facts[concept.Key] = rows.EnumerateArray().Select(ParseFact).OfType<SecFact>().ToList();
                }
            }
        }

        return new SecCompanyFacts(facts, hasIfrs);
    }

    /// <summary>
    /// The property <paramref name="name"/> of <paramref name="element"/>; false when it is missing or the element is
    /// not an object, where <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> would throw.
    /// </summary>
    private static bool TryGetObjectProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
    }

    private static SecFact? ParseFact(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object
            || !TryGetDate(row, "end", out DateOnly end)
            || !TryGetDate(row, "filed", out DateOnly filed)
            || !row.TryGetProperty("val", out JsonElement val)
            || val.ValueKind != JsonValueKind.Number
            || !val.TryGetDecimal(out decimal value)
            || GetString(row, "form") is not { } form
            || GetString(row, "accn") is not { } accession)
        {
            return null;
        }

        DateOnly? start = TryGetDate(row, "start", out DateOnly s) ? s : null;
        // SEC leaves fy null on facts from some filings; TryGetInt32 throws on anything but a number.
        int? fiscalYear = row.TryGetProperty("fy", out JsonElement fy) && fy.ValueKind == JsonValueKind.Number
                                                                       && fy.TryGetInt32(out int y)
            ? y
            : null;
        return new SecFact(start, end, value, fiscalYear, GetString(row, "fp"), form, filed, accession);
    }

    private static string? GetString(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? BlankToNull(element.GetString())
            : null;

    private static bool TryGetDate(JsonElement row, string name, out DateOnly date)
    {
        date = default;
        return GetString(row, name) is { } text
               && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                   out date);
    }

    /// <summary>The submissions document as the profile reads it; filings missing a form or date are skipped.</summary>
    private static SecCompanySubmissions ToSubmissions(long cik, CompanySubmissionsResponse response)
    {
        RecentFilingsResponse recent = response.Filings?.Recent ?? new RecentFilingsResponse();
        var filings = new List<SecFilingEntry>();
        int count = recent.AccessionNumber?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            string? accession = BlankToNull(recent.AccessionNumber![i]);
            string? form = BlankToNull(At(recent.Form, i));
            if (accession is null || form is null
                                  || !DateOnly.TryParseExact(At(recent.FilingDate, i), "yyyy-MM-dd",
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out DateOnly filedOn))
            {
                continue;
            }

            DateOnly? reportedOn = DateOnly.TryParseExact(At(recent.ReportDate, i), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly r)
                ? r
                : null;
            List<string> items = (At(recent.Items, i) ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            filings.Add(new SecFilingEntry(accession, form, filedOn, reportedOn, items));
        }

        return new SecCompanySubmissions(
            cik,
            response.Name ?? string.Empty,
            response.Tickers ?? [],
            (response.Exchanges ?? []).Select(BlankToNull).ToList(),
            BlankToNull(response.Sic),
            BlankToNull(response.SicDescription),
            BlankToNull(response.OwnerOrg),
            BlankToNull(response.StateOfIncorporationDescription) ?? BlankToNull(response.StateOfIncorporation),
            BlankToNull(response.FiscalYearEnd),
            filings);
    }

    private static string? At(List<string?>? values, int index) =>
        values is not null && index < values.Count ? values[index] : null;

    /// <summary>
    /// Sends one GET to SEC once the shared gate allows it, with the configured User-Agent, returning as soon as the
    /// headers arrive so large documents are read as a stream.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _settings.UserAgent);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SubmissionsResponse
    {
        public string? Name { get; set; }
        public string? Sic { get; set; }
        public string? SicDescription { get; set; }
        public string? OwnerOrg { get; set; }
        public List<string>? Tickers { get; set; }
    }

    private sealed class CompanySubmissionsResponse
    {
        public string? Name { get; set; }
        public string? Sic { get; set; }
        public string? SicDescription { get; set; }
        public string? OwnerOrg { get; set; }
        public string? StateOfIncorporation { get; set; }
        public string? StateOfIncorporationDescription { get; set; }
        public string? FiscalYearEnd { get; set; }
        public List<string>? Tickers { get; set; }
        public List<string?>? Exchanges { get; set; }
        public FilingsResponse? Filings { get; set; }
    }

    private sealed class FilingsResponse
    {
        public RecentFilingsResponse? Recent { get; set; }
    }

    // EDGAR lists recent filings as parallel arrays, one entry per filing in each.
    private sealed class RecentFilingsResponse
    {
        public List<string?>? AccessionNumber { get; set; }
        public List<string?>? FilingDate { get; set; }
        public List<string?>? ReportDate { get; set; }
        public List<string?>? Form { get; set; }
        public List<string?>? Items { get; set; }
    }

    private sealed class FrameResponse
    {
        public List<FrameRow>? Data { get; set; }
    }

    private sealed class FrameRow
    {
        public long Cik { get; set; }
        public string? EntityName { get; set; }
        public string? End { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Val { get; set; }
    }
}
