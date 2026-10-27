using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="IFrankfurterClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>. Frankfurter answers
/// <c>/v2/rates?base=USD&amp;quotes=VND&amp;from=…&amp;to=…</c> with a JSON array of
/// <c>{ "date", "base", "quote", "rate" }</c>, one row per calendar day.
/// </summary>
internal sealed class FrankfurterClient : IFrankfurterClient
{
    private const string BaseUrl = "https://api.frankfurter.dev/v2/rates";

    private readonly HttpClient _httpClient;

    public FrankfurterClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyRate>> GetUsdRatesAsync(string quoteCurrency, DateOnly from, DateOnly to,
        CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}?base=USD&quotes={Uri.EscapeDataString(quoteCurrency)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        List<Row>? rows;
        try
        {
            rows = await _httpClient.GetFromJsonAsync<List<Row>>(url, JsonSerializerOptions.Web, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Frankfurter answered something that is not a list of rates.", ex);
        }

        return Parse(rows, quoteCurrency);
    }

    /// <summary>
    /// The rows that are rates from USD to <paramref name="quoteCurrency"/>, by date. Throws
    /// <see cref="InvalidDataException"/> for a row with another pair or a rate that is not positive.
    /// </summary>
    internal static IReadOnlyList<DailyRate> Parse(IReadOnlyList<Row>? rows, string quoteCurrency)
    {
        if (rows is null)
        {
            throw new InvalidDataException("Frankfurter answered no rates.");
        }

        var rates = new List<DailyRate>(rows.Count);
        foreach (Row row in rows)
        {
            if (!string.Equals(row.Base, "USD", StringComparison.Ordinal)
                || !string.Equals(row.Quote, quoteCurrency, StringComparison.Ordinal)
                || row.Rate <= 0m
                || !DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateOnly date))
            {
                throw new InvalidDataException("Frankfurter answered a row that is not a USD rate on a date.");
            }

            rates.Add(new DailyRate(date, row.Rate));
        }

        return rates;
    }

    /// <summary>One row of Frankfurter's answer.</summary>
    internal sealed record Row(string? Date, string? Base, string? Quote, decimal Rate);
}
