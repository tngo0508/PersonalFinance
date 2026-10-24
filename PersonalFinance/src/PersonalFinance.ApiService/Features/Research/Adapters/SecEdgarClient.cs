using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="ISecEdgarClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>. Every request carries the
/// configured User-Agent and first waits its turn at the shared <see cref="SecRequestGate"/>.
/// </summary>
internal sealed class SecEdgarClient : ISecEdgarClient
{
    private const string BaseUrl = "https://data.sec.gov/";

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
        string url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}api/xbrl/frames/dei/EntityPublicFloat/USD/CY{year}Q{quarter}I.json");

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

        var facts = new List<PublicFloatFact>();
        foreach (FrameRow row in frame?.Data ?? [])
        {
            if (row.Val is { } value
                && DateOnly.TryParseExact(row.End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateOnly end))
            {
                facts.Add(new PublicFloatFact(row.Cik, row.EntityName ?? string.Empty, end, value));
            }
        }

        return facts;
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
