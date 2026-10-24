using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <inheritdoc />
internal sealed class FedRatesClient : IFedRatesClient
{
    public const string BundleUrl = "https://www.federalreserve.gov/releases/h15/data/FRB_h15_xml.zip";

    // The 3-month Treasury bill, secondary market, monthly average.
    private const string TreasuryBillSeries = "RIFSGFSM03_N.M";
    private const string DataEntry = "H15_data.xml";

    // A monthly observation is dated at its month's end; a bare month is read too.
    private static readonly string[] PeriodFormats = ["yyyy-MM-dd", "yyyy-MM"];

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

    public FedRatesClient(HttpClient httpClient, SecEdgarSettings secSettings)
    {
        _httpClient = httpClient;
        _secSettings = secSettings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BundleUrl);
        // The same contact the SEC requests carry, so the Board can reach the operator.
        if (!string.IsNullOrWhiteSpace(_secSettings.UserAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", _secSettings.UserAgent);
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var zip = new ZipArchive(body, ZipArchiveMode.Read);
        ZipArchiveEntry entry = zip.GetEntry(DataEntry)
            ?? throw new InvalidDataException($"The H.15 bundle has no {DataEntry}.");
        await using Stream data = entry.Open();
        return await ReadSeriesAsync(data, TreasuryBillSeries, cancellationToken);
    }

    /// <summary>
    /// The monthly observations of one series of an H.15 data document (<c>Series SERIES_NAME="..."</c> holding
    /// <c>Obs TIME_PERIOD="2026-09-30" OBS_VALUE="3.94"</c>, the period dated at its month's end), stopping at the end
    /// of that series. Observations that are not a number (e.g. "ND", no data) are left out.
    /// </summary>
    internal static async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> ReadSeriesAsync(Stream data,
        string seriesName, CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(data, ReaderSettings);
        var rates = new List<(DateOnly Month, decimal Rate)>();
        bool inSeries = false;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Series")
            {
                if (inSeries)
                {
                    break;
                }

                inSeries = reader.GetAttribute("SERIES_NAME") == seriesName;
            }
            else if (inSeries && reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "Series")
            {
                break;
            }
            else if (inSeries && reader.NodeType == XmlNodeType.Element && reader.LocalName == "Obs"
                     && DateOnly.TryParseExact(reader.GetAttribute("TIME_PERIOD"), PeriodFormats,
                         CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly period)
                     && decimal.TryParse(reader.GetAttribute("OBS_VALUE"), NumberStyles.Float,
                         CultureInfo.InvariantCulture, out decimal rate))
            {
                rates.Add((new DateOnly(period.Year, period.Month, 1), rate));
            }
        }

        return rates.OrderBy(r => r.Month).ToList();
    }
}
