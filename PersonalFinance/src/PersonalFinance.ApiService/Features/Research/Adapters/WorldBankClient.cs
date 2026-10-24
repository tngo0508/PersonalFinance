using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// <see cref="IWorldBankClient"/> over HTTP, registered as a typed <see cref="HttpClient"/>. The WDI API answers a JSON
/// array: a paging header, then the rows; a bad request is a one-element array holding a <c>message</c>.
/// </summary>
internal sealed class WorldBankClient : IWorldBankClient
{
    private const string BaseUrl = "https://api.worldbank.org/v2/";

    // Large pages keep a full indicator (about 270 countries and aggregates a year) to a handful of requests.
    private const int CountriesPerPage = 1_000;
    private const int ObservationsPerPage = 20_000;

    // The aggregates' region id in the country list.
    private const string AggregateRegion = "NA";

    // Stops a paging header that never ends.
    private const int MaxPages = 100;

    private readonly HttpClient _httpClient;

    public WorldBankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorldBankCountry>> GetCountriesAsync(CancellationToken cancellationToken)
    {
        var countries = new List<WorldBankCountry>();
        await foreach ((PageHeader _, JsonElement row) in ReadPagesAsync(
                           string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}country?format=json&per_page={CountriesPerPage}"),
                           cancellationToken))
        {
            CountryRow? country = row.Deserialize<CountryRow>(JsonSerializerOptions.Web);
            if (!string.IsNullOrWhiteSpace(country?.Id))
            {
                countries.Add(new WorldBankCountry(country.Id.Trim(), country.Name?.Trim() ?? country.Id.Trim(),
                    string.Equals(country.Region?.Id, AggregateRegion, StringComparison.Ordinal)));
            }
        }

        return countries;
    }

    /// <inheritdoc />
    public async Task<WorldBankIndicator> GetIndicatorAsync(string indicator, int firstYear, int lastYear,
        CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}country/all/indicator/{Uri.EscapeDataString(indicator)}?format=json&per_page={ObservationsPerPage}&date={firstYear}:{lastYear}");
        DateOnly? lastUpdated = null;
        var observations = new List<WorldBankObservation>();
        await foreach ((PageHeader header, JsonElement row) in ReadPagesAsync(url, cancellationToken))
        {
            if (lastUpdated is null
                && DateOnly.TryParseExact(header.LastUpdated, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly updated))
            {
                lastUpdated = updated;
            }

            ObservationRow? observation = row.Deserialize<ObservationRow>(JsonSerializerOptions.Web);
            if (!string.IsNullOrWhiteSpace(observation?.CountryIso3Code)
                && int.TryParse(observation.Date, NumberStyles.None, CultureInfo.InvariantCulture, out int year))
            {
                observations.Add(new WorldBankObservation(observation.CountryIso3Code.Trim(), year, observation.Value));
            }
        }

        return new WorldBankIndicator(lastUpdated, observations);
    }

    /// <summary>
    /// Reads every page of a WDI list, yielding each row with its page's header. Throws when a response is not a
    /// success, not the expected two-element array (e.g. an error message), or keeps paging past
    /// <see cref="MaxPages"/>.
    /// </summary>
    private async IAsyncEnumerable<(PageHeader Header, JsonElement Row)> ReadPagesAsync(string url,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int pages = 1;
        for (int page = 1; page <= pages; page++)
        {
            if (page > MaxPages)
            {
                throw new InvalidOperationException("The World Bank answered more pages than expected.");
            }

            using HttpResponseMessage response = await _httpClient.GetAsync(
                string.Create(CultureInfo.InvariantCulture, $"{url}&page={page}"),
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2
                || root[0].ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The World Bank answered with an error instead of data.");
            }

            PageHeader header = root[0].Deserialize<PageHeader>(JsonSerializerOptions.Web) ?? new PageHeader();
            pages = Math.Max(1, header.Pages);
            if (root[1].ValueKind != JsonValueKind.Array)
            {
                continue; // a page with no rows is null
            }

            foreach (JsonElement row in root[1].EnumerateArray())
            {
                yield return (header, row.Clone());
            }
        }
    }

    private sealed class PageHeader
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Pages { get; set; }

        public string? LastUpdated { get; set; }
    }

    private sealed class CountryRow
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public RegionRef? Region { get; set; }
    }

    private sealed class RegionRef
    {
        public string? Id { get; set; }
    }

    private sealed class ObservationRow
    {
        public string? CountryIso3Code { get; set; }
        public string? Date { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Value { get; set; }
    }
}
