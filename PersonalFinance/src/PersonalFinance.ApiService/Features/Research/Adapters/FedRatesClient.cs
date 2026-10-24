using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <inheritdoc />
internal sealed class FedRatesClient : IFedRatesClient
{
    public const string BundleUrl = "https://www.federalreserve.gov/releases/h15/data/FRB_h15_xml.zip";

    /// <summary>The effective federal funds rate, monthly average.</summary>
    public const string FedFundsSeries = "RIFSPFF_N.M";

    /// <summary>The 3-month Treasury bill, secondary market, monthly average.</summary>
    public const string TreasuryBillSeries = "RIFSGFSM03_N.M";

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
        CancellationToken cancellationToken) =>
        (await GetMonthlyRatesAsync(cancellationToken)).TreasuryBill;

    /// <inheritdoc />
    public async Task<FedMonthlyRates> GetMonthlyRatesAsync(CancellationToken cancellationToken)
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
        IReadOnlyDictionary<string, IReadOnlyList<(DateOnly Month, decimal Rate)>> series =
            await ReadSeriesAsync(data, [FedFundsSeries, TreasuryBillSeries], cancellationToken);
        return new FedMonthlyRates(series[FedFundsSeries], series[TreasuryBillSeries]);
    }

    /// <summary>
    /// Whether an observation holds a value: its status is "A" (or absent), and its value is not the -9999 that H.15
    /// gives a day with no data.
    /// </summary>
    private static bool IsObserved(string? status, string? value) =>
        (status is null || string.Equals(status, "A", StringComparison.Ordinal)) && value is not null
        && !value.StartsWith("-9999", StringComparison.Ordinal);

    /// <summary>The monthly observations of one series of an H.15 data document; see the overload for several.</summary>
    internal static async Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> ReadSeriesAsync(Stream data,
        string seriesName, CancellationToken cancellationToken) =>
        (await ReadSeriesAsync(data, [seriesName], cancellationToken))[seriesName];

    /// <summary>
    /// The monthly observations of the named series of an H.15 data document (<c>Series SERIES_NAME="..."</c> holding
    /// <c>Obs TIME_PERIOD="2026-09-30" OBS_VALUE="3.94"</c>, the period dated at its month's end), in one pass that
    /// stops once every named series has been read. Each is keyed by the first day of its month, oldest first;
    /// observations with no data (status other than "A", the value -9999, or not a number) are left out, and a series
    /// not in the document is empty.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<(DateOnly Month, decimal Rate)>>> ReadSeriesAsync(
        Stream data, IReadOnlyCollection<string> seriesNames, CancellationToken cancellationToken)
    {
        var found = seriesNames.ToDictionary(n => n, _ => new List<(DateOnly Month, decimal Rate)>(), StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        using var reader = XmlReader.Create(data, ReaderSettings);
        List<(DateOnly Month, decimal Rate)>? current = null;
        string? currentName = null;
        while (done.Count < found.Count && await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Series")
            {
                currentName = reader.GetAttribute("SERIES_NAME");
                current = currentName is not null && found.TryGetValue(currentName, out List<(DateOnly, decimal)>? list)
                    ? list
                    : null;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "Series")
            {
                if (current is not null)
                {
                    done.Add(currentName!);
                }

                current = null;
            }
            else if (current is not null && reader.NodeType == XmlNodeType.Element && reader.LocalName == "Obs"
                     && IsObserved(reader.GetAttribute("OBS_STATUS"), reader.GetAttribute("OBS_VALUE"))
                     && DateOnly.TryParseExact(reader.GetAttribute("TIME_PERIOD"), PeriodFormats,
                         CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly period)
                     && decimal.TryParse(reader.GetAttribute("OBS_VALUE"), NumberStyles.Float,
                         CultureInfo.InvariantCulture, out decimal rate))
            {
                current.Add((new DateOnly(period.Year, period.Month, 1), rate));
            }
        }

        return found.ToDictionary(p => p.Key,
            p => (IReadOnlyList<(DateOnly Month, decimal Rate)>)p.Value.OrderBy(r => r.Month).ToList(), StringComparer.Ordinal);
    }
}
