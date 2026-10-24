using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="ISecEdgarClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>. Every request carries the
/// configured User-Agent and first waits its turn at the shared <see cref="SecRequestGate"/>.
/// </summary>
internal sealed class SecEdgarClient : ISecEdgarClient
{
    private const string BaseUrl = "https://data.sec.gov/";

    // The fund ticker list, EDGAR's filing lists and the filing archives are served from www.sec.gov.
    private const string WwwUrl = "https://www.sec.gov/";

    // The most filings EDGAR's company browse feed lists on one page.
    private const int FilingListPageSize = 100;

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
        using HttpResponseMessage response = await SendAsync($"{WwwUrl}files/company_tickers_mf.json", cancellationToken);
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
                series.Add(new SecFundSeries(cik, seriesId, ticker));
            }
        }

        return series;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NportFilingRef>> GetNportFilingsAsync(string filer, DateOnly filedFrom,
        CancellationToken cancellationToken)
    {
        var filings = new List<NportFilingRef>();
        for (int start = 0; ; start += FilingListPageSize)
        {
            // EDGAR's type filter is a prefix match, so NPORT-P also lists the NPORT-P/A amendments.
            string url = string.Create(CultureInfo.InvariantCulture,
                $"{WwwUrl}cgi-bin/browse-edgar?action=getcompany&CIK={Uri.EscapeDataString(filer)}&type=NPORT-P&dateb=&owner=include&start={start}&count={FilingListPageSize}&output=atom");
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
