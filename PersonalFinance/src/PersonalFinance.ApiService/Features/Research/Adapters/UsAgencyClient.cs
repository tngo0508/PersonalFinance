using System.Globalization;
using System.Xml;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <inheritdoc />
internal sealed class UsAgencyClient : IUsAgencyClient
{
    /// <summary>The BLS flat file of every all-items CPI series, a few MB of tab-separated text.</summary>
    public const string CpiUrl = "https://download.bls.gov/pub/time.series/cu/cu.data.1.AllItems";

    /// <summary>CPI-U, all items, US city average, not seasonally adjusted.</summary>
    public const string CpiSeries = "CUUR0000SA0";

    private const string TreasuryBillUrl =
        "https://home.treasury.gov/resource-center/data-chart-center/interest-rates/pages/xml?data=daily_treasury_bill_rates&field_tdr_date_value=";

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private readonly HttpClient _httpClient;
    private readonly SecEdgarSettings _secSettings;

    public UsAgencyClient(HttpClient httpClient, SecEdgarSettings secSettings)
    {
        _httpClient = httpClient;
        _secSettings = secSettings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(DateOnly Month, decimal Index)>> GetConsumerPricesAsync(int firstYear,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(CpiUrl, cancellationToken);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body);
        return await ParseConsumerPricesAsync(reader, firstYear, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(int year,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            string.Create(CultureInfo.InvariantCulture, $"{TreasuryBillUrl}{year}"), cancellationToken);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ParseTreasuryBillRatesAsync(body, cancellationToken);
    }

    /// <summary>
    /// The CPI-U rows of the BLS flat file (tab-separated <c>series_id, year, period, value, footnote_codes</c>, the
    /// series id padded with spaces): monthly periods <c>M01</c> to <c>M12</c> from <paramref name="firstYear"/>, the
    /// annual average (<c>M13</c>) and other series left out.
    /// </summary>
    internal static async Task<IReadOnlyList<(DateOnly Month, decimal Index)>> ParseConsumerPricesAsync(TextReader reader,
        int firstYear, CancellationToken cancellationToken)
    {
        var prices = new List<(DateOnly Month, decimal Index)>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            string[] fields = line.Split('\t');
            if (fields.Length < 4 || fields[0].Trim() != CpiSeries
                || !int.TryParse(fields[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int year)
                || year < firstYear)
            {
                continue;
            }

            string period = fields[2].Trim();
            if (period.Length == 3 && period[0] == 'M'
                && int.TryParse(period.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int month)
                && month is >= 1 and <= 12
                && decimal.TryParse(fields[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal index))
            {
                prices.Add((new DateOnly(year, month, 1), index));
            }
        }

        return prices.OrderBy(p => p.Month).ToList();
    }

    /// <summary>
    /// The Treasury's daily bill rates feed for one year (an Atom feed whose entries hold <c>d:INDEX_DATE</c> and the
    /// 13-week close <c>d:ROUND_B1_CLOSE_13WK_2</c>), averaged per month.
    /// </summary>
    internal static async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> ParseTreasuryBillRatesAsync(Stream feed,
        CancellationToken cancellationToken)
    {
        var daily = new List<(DateOnly Month, decimal Rate)>();
        using var reader = XmlReader.Create(feed, ReaderSettings);
        DateOnly? date = null;
        // Reading an element's content leaves the reader on the next node already, so the loop must not read again.
        bool advance = true;
        while (!advance || await reader.ReadAsync())
        {
            advance = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.EOF || reader.NodeType != XmlNodeType.Element)
            {
                if (reader.EOF)
                {
                    break;
                }

                continue;
            }

            if (reader.LocalName == "INDEX_DATE")
            {
                string text = await reader.ReadElementContentAsStringAsync();
                advance = false;
                date = DateTime.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
                    ? new DateOnly(parsed.Year, parsed.Month, 1)
                    : null;
            }
            else if (reader.LocalName == "ROUND_B1_CLOSE_13WK_2")
            {
                string text = await reader.ReadElementContentAsStringAsync();
                advance = false;
                if (date is { } month
                    && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate))
                {
                    daily.Add((month, rate));
                }
            }
        }

        return daily
            .GroupBy(d => d.Month)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, Math.Round(g.Average(d => d.Rate), 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    /// <summary>A GET with the operator's contact as User-Agent (BLS refuses requests without one).</summary>
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_secSettings.UserAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", _secSettings.UserAgent);
        }

        HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
