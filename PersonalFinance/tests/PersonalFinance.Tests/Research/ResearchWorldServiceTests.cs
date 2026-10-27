using System.Net;
using System.Text;
using System.Web;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Feature's World snapshot, built through the real World Bank client over a stub HTTP handler that
/// answers with canned WDI responses, and stored in an InMemory database: per year, each country's market
/// capitalisation and market cap as % of GDP, keyed by ISO numeric code to match the world map.
/// </summary>
public class ResearchWorldServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private const string CountriesJson = """
        [{"page":1,"pages":1,"per_page":"1000","total":7},[
          {"id":"USA","iso2Code":"US","name":"United States","region":{"id":"NAC","iso2code":"XU","value":"North America"}},
          {"id":"JPN","iso2Code":"JP","name":"Japan","region":{"id":"EAS","iso2code":"Z4","value":"East Asia & Pacific"}},
          {"id":"FRA","iso2Code":"FR","name":"France","region":{"id":"ECS","iso2code":"Z7","value":"Europe & Central Asia"}},
          {"id":"WLD","iso2Code":"1W","name":"World","region":{"id":"NA","iso2code":"NA","value":"Aggregates"}},
          {"id":"EUU","iso2Code":"EU","name":"European Union","region":{"id":"NA","iso2code":"NA","value":"Aggregates"}},
          {"id":"XKX","iso2Code":"XK","name":"Kosovo","region":{"id":"ECS","iso2code":"Z7","value":"Europe & Central Asia"}},
          {"id":"BRA","iso2Code":"BR","name":"Brazil","region":{"id":"LCN","iso2code":"ZJ","value":"Latin America & Caribbean "}}
        ]]
        """;

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db, StubWorldBankHandler handler)
    {
        var secSettings = new SecEdgarSettings { UserAgent = "PersonalFinance tests contact@example.com", MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        // SEC has nothing: the Sectors dataset fails on its own and the World dataset is built regardless.
        SecEdgarClient secClient = ResearchServiceFactory.SecClient(new DelegatingTestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))), secSettings);
        var worldClient = new WorldBankClient(new HttpClient(handler));
        var research = new ResearchSettings { SectorsFirstYear = 2025, WorldFirstYear = 2024 };
        return ResearchServiceFactory.Create(db, secClient, worldClient, research, timeProvider);
    }

    private static StubWorldBankHandler CannedHandler()
    {
        var handler = new StubWorldBankHandler { Countries = CountriesJson };
        // Market cap over two pages, so the client must follow the paging.
        handler.Indicators["CM.MKT.LCAP.CD"] =
        [
            """
            [{"page":1,"pages":2,"per_page":4,"total":8,"sourceid":"2","lastupdated":"2026-10-08"},[
              {"countryiso3code":"USA","date":"2025","value":null},
              {"countryiso3code":"USA","date":"2024","value":5.0E13},
              {"countryiso3code":"JPN","date":"2025","value":null},
              {"countryiso3code":"JPN","date":"2024","value":6500000000000}
            ]]
            """,
            """
            [{"page":2,"pages":2,"per_page":4,"total":8,"sourceid":"2","lastupdated":"2026-10-08"},[
              {"countryiso3code":"FRA","date":"2025","value":3000000000000.4},
              {"countryiso3code":"WLD","date":"2024","value":110000000000000},
              {"countryiso3code":"XKX","date":"2024","value":1000000000},
              {"countryiso3code":"BRA","date":"2024","value":0}
            ]]
            """,
        ];
        handler.Indicators["NY.GDP.MKTP.CD"] =
        [
            """
            [{"page":1,"pages":1,"per_page":20000,"total":5,"sourceid":"2","lastupdated":"2026-09-30"},[
              {"countryiso3code":"USA","date":"2024","value":29000000000000},
              {"countryiso3code":"JPN","date":"2024","value":4000000000000},
              {"countryiso3code":"FRA","date":"2025","value":null},
              {"countryiso3code":"BRA","date":"2024","value":2200000000000},
              {"countryiso3code":"WLD","date":"2024","value":110000000000000}
            ]]
            """,
        ];
        return handler;
    }

    private static async Task<WorldSnapshotDto> RefreshAndReadAsync(ResearchService service)
    {
        ResearchRefreshSummary summary = await service.RefreshAsync();
        Assert.Contains("world", summary.Built);
        return (await service.GetWorldAsync()).Snapshot!;
    }

    private static Dictionary<string, (decimal MarketCap, decimal? PercentOfGdp)> YearOf(WorldSnapshotDto snapshot,
        string period) =>
        snapshot.Values.Single(v => v.Period == period).Countries
            .ToDictionary(c => snapshot.Countries[c.Country].Id, c => (c.MarketCap, c.PercentOfGdp));

    [Fact]
    public async Task RefreshAsync_KeysEachCountryByItsIsoNumericCode_AndDropsAggregatesAndUnmappedCountries()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");

        WorldSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, CannedHandler()));

        // World and European Union are aggregates; Kosovo (XKX) has no ISO 3166 numeric code, so no map shape.
        Assert.Equal([("076", "BRA", "Brazil"), ("250", "FRA", "France"), ("392", "JPN", "Japan"), ("840", "USA", "United States")],
            snapshot.Countries.Select(c => (c.Id, c.Iso3, c.Name)));
        Assert.Equal(["2024", "2025"], snapshot.Periods);
        Assert.Equal(new DateOnly(2026, 10, 8), snapshot.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_LeavesAbsentValuesAbsent_NeverZero()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");

        WorldSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, CannedHandler()));

        // 2025: the United States and Japan have no market cap, so they are not listed at all.
        Assert.Equal(["250"], YearOf(snapshot, "2025").Keys);
        // A reported zero is a value, kept as reported.
        Assert.Equal(0m, YearOf(snapshot, "2024")["076"].MarketCap);
    }

    [Fact]
    public async Task RefreshAsync_ComputesPercentOfGdp_OnlyWhenGdpExists()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");

        WorldSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, CannedHandler()));

        Dictionary<string, (decimal MarketCap, decimal? PercentOfGdp)> year2024 = YearOf(snapshot, "2024");
        Assert.Equal((50_000_000_000_000m, 172.4m), year2024["840"]); // 50T / 29T
        Assert.Equal((6_500_000_000_000m, 162.5m), year2024["392"]);
        Assert.Equal(0m, year2024["076"].PercentOfGdp);
        // France has a 2025 market cap but no 2025 GDP: market cap kept (to the dollar), % of GDP absent.
        Assert.Equal((3_000_000_000_000m, (decimal?)null), YearOf(snapshot, "2025")["250"]);
    }

    [Fact]
    public async Task RefreshAsync_ListsEachYearsCountries_LargestMarketCapFirst()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");

        WorldSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, CannedHandler()));

        Assert.Equal(["840", "392", "076"],
            snapshot.Values[0].Countries.Select(c => snapshot.Countries[c.Country].Id));
    }

    [Fact]
    public async Task RefreshAsync_AsksForEveryCountry_FromTheFirstYearToLastYear()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");
        StubWorldBankHandler handler = CannedHandler();

        await RefreshAndReadAsync(CreateService(db, handler));

        List<Uri> indicatorRequests = handler.Requests.Where(r => r.AbsolutePath.Contains("/indicator/", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, indicatorRequests.Count); // two pages of market cap, one of GDP
        Assert.All(indicatorRequests, uri =>
        {
            Assert.StartsWith("/v2/country/all/indicator/", uri.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal("2024:2025", HttpUtility.ParseQueryString(uri.Query)["date"]);
        });
    }

    [Fact]
    public async Task RefreshAsync_WhenTheWorldBankFails_KeepsThePreviousSnapshot()
    {
        string database = $"World_{Guid.NewGuid():N}";
        StubWorldBankHandler handler = CannedHandler();
        await using (AppDbContext db = CreateContext(database))
        {
            await RefreshAndReadAsync(CreateService(db, handler));
        }

        handler.Fail = true;
        await using AppDbContext later = CreateContext(database);
        ResearchService service = CreateService(later, handler);
        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains("world", summary.Failed);
        Assert.DoesNotContain("world", summary.Built);
        Assert.Equal(new DateOnly(2026, 10, 8), (await service.GetWorldAsync()).Snapshot!.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_WhenTheWorldBankAnswersWithAnErrorMessage_StaysNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"World_{Guid.NewGuid():N}");
        StubWorldBankHandler handler = CannedHandler();
        // The WDI API answers a bad request with 200 and a one-element array holding the message.
        handler.Indicators["CM.MKT.LCAP.CD"] =
            ["""[{"message":[{"id":"120","key":"Invalid value","value":"The provided parameter value is not valid"}]}]"""];

        ResearchService service = CreateService(db, handler);
        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains("world", summary.Failed);
        Assert.Null((await service.GetWorldAsync()).Snapshot);
    }

    /// <summary>
    /// Answers the WDI API from canned bodies: the country list, and each indicator's pages in order (the
    /// <c>page</c> query parameter picks one). Anything else is a 404.
    /// </summary>
    private sealed class StubWorldBankHandler : HttpMessageHandler
    {
        public string Countries { get; set; } = "[]";
        public Dictionary<string, List<string>> Indicators { get; } = new();
        public bool Fail { get; set; }
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            Requests.Add(uri);
            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            string? body = null;
            const string indicatorPrefix = "/v2/country/all/indicator/";
            if (uri.Host == "api.worldbank.org" && uri.AbsolutePath == "/v2/country")
            {
                body = Countries;
            }
            else if (uri.Host == "api.worldbank.org"
                     && uri.AbsolutePath.StartsWith(indicatorPrefix, StringComparison.Ordinal)
                     && Indicators.TryGetValue(uri.AbsolutePath[indicatorPrefix.Length..], out List<string>? pages))
            {
                int page = int.TryParse(HttpUtility.ParseQueryString(uri.Query)["page"], out int p) ? p : 1;
                body = page >= 1 && page <= pages.Count ? pages[page - 1] : null;
            }

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
