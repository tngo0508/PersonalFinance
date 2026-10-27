using System.Globalization;
using System.Text.Json;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="ICurrencyApiClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>. The currency-api answers
/// <c>…/v1/currencies/usd.min.json</c> with <c>{ "date": "2026-10-25", "usd": { "vnd": 25831.81, … } }</c>; its author
/// asks consumers to fall back to the Cloudflare Pages mirror, so a failed jsDelivr read tries that next.
/// </summary>
internal sealed class CurrencyApiClient : ICurrencyApiClient
{
    internal static readonly string[] Urls =
    [
        "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json",
        "https://latest.currency-api.pages.dev/v1/currencies/usd.min.json"
    ];

    private readonly HttpClient _httpClient;

    public CurrencyApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<DailyRate> GetLatestUsdRateAsync(string quoteCurrency, CancellationToken cancellationToken)
    {
        Exception? last = null;
        foreach (string url in Urls)
        {
            try
            {
                await using Stream body = await _httpClient.GetStreamAsync(url, cancellationToken);
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                return Parse(document.RootElement, quoteCurrency);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException
                                           || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                last = ex;
            }
        }

        throw last is InvalidDataException or HttpRequestException
            ? last
            : new HttpRequestException("The currency-api could not be read.", last);
    }

    /// <summary>
    /// The rate to <paramref name="quoteCurrency"/> in a currency-api answer. Throws <see cref="InvalidDataException"/>
    /// when the answer has no date or no positive rate for it.
    /// </summary>
    internal static DailyRate Parse(JsonElement root, string quoteCurrency)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("date", out JsonElement dateElement)
            && dateElement.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(dateElement.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date)
            && root.TryGetProperty("usd", out JsonElement usd)
            && usd.ValueKind == JsonValueKind.Object
            && usd.TryGetProperty(quoteCurrency.ToLowerInvariant(), out JsonElement rateElement)
            && rateElement.ValueKind == JsonValueKind.Number
            && rateElement.TryGetDecimal(out decimal rate)
            && rate > 0m)
        {
            return new DailyRate(date, rate);
        }

        throw new InvalidDataException($"The currency-api answered no USD to {quoteCurrency} rate.");
    }
}
