using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The daily exchange-rate refresh (ADR 0016, localization spec §2.6): Frankfurter first, from the day after its latest
/// stored rate (or the first date) to today; the currency-api's latest rate when Frankfurter fails; rows only added.
/// </summary>
public class FxRatesRefresherTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 26);

    [Fact]
    public async Task AnEmptyTable_IsFilledFromTheFirstDate_ToToday_FromFrankfurter()
    {
        await using AppDbContext db = CreateContext();
        var frankfurter = new FakeFrankfurter();

        await Refresher(db, frankfurter, new FakeCurrencyApi()).RefreshAsync(CancellationToken.None);

        (string Quote, DateOnly From, DateOnly To) asked = Assert.Single(frankfurter.Requests);
        Assert.Equal(("VND", FxRatesRefresher.FirstDate, Today), asked);
        List<ExchangeRate> rows = await db.ExchangeRates.OrderBy(r => r.Date).ToListAsync();
        Assert.Equal(Today.DayNumber - FxRatesRefresher.FirstDate.DayNumber + 1, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("USD", row.BaseCurrency);
            Assert.Equal("VND", row.QuoteCurrency);
            Assert.Equal(FxRatesRefresher.FrankfurterSource, row.Source);
        });
    }

    [Fact]
    public async Task StoredRates_AreContinuedFromTheDayAfterTheLatest()
    {
        await using AppDbContext db = CreateContext();
        db.ExchangeRates.Add(Rate(new DateOnly(2026, 10, 23), 25_800m, FxRatesRefresher.FrankfurterSource));
        await db.SaveChangesAsync();
        var frankfurter = new FakeFrankfurter();

        await Refresher(db, frankfurter, new FakeCurrencyApi()).RefreshAsync(CancellationToken.None);

        Assert.Equal(("VND", new DateOnly(2026, 10, 24), Today), Assert.Single(frankfurter.Requests));
        Assert.Equal(4, await db.ExchangeRates.CountAsync());
        Assert.Equal(25_800m, (await db.ExchangeRates.SingleAsync(r => r.Date == new DateOnly(2026, 10, 23))).Rate);
    }

    [Fact]
    public async Task UpToDateRates_AskNoSource()
    {
        await using AppDbContext db = CreateContext();
        db.ExchangeRates.Add(Rate(Today, 25_818m, FxRatesRefresher.FrankfurterSource));
        await db.SaveChangesAsync();
        var frankfurter = new FakeFrankfurter();
        var currencyApi = new FakeCurrencyApi();

        await Refresher(db, frankfurter, currencyApi).RefreshAsync(CancellationToken.None);

        Assert.Empty(frankfurter.Requests);
        Assert.Equal(0, currencyApi.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenFrankfurterFailsOrHasNone_TheCurrencyApisLatestRateIsStored(bool frankfurterAnswersNothing)
    {
        await using AppDbContext db = CreateContext();
        var frankfurter = new FakeFrankfurter
        {
            Failure = frankfurterAnswersNothing ? null : new HttpRequestException("down"),
            AnswersNothing = frankfurterAnswersNothing
        };

        await Refresher(db, frankfurter, new FakeCurrencyApi { Latest = new DailyRate(Today, 25_831.81m) })
            .RefreshAsync(CancellationToken.None);

        ExchangeRate stored = await db.ExchangeRates.SingleAsync();
        Assert.Equal((Today, 25_831.81m, FxRatesRefresher.CurrencyApiSource), (stored.Date, stored.Rate, stored.Source));
    }

    [Fact]
    public async Task AfterAFallbackDay_FrankfurterFillsTheDaysInBetween_AndKeepsTheFallbackRate()
    {
        await using AppDbContext db = CreateContext();
        db.ExchangeRates.AddRange(
            Rate(new DateOnly(2026, 10, 22), 25_800m, FxRatesRefresher.FrankfurterSource),
            Rate(new DateOnly(2026, 10, 25), 25_831.81m, FxRatesRefresher.CurrencyApiSource));
        await db.SaveChangesAsync();
        var frankfurter = new FakeFrankfurter();

        await Refresher(db, frankfurter, new FakeCurrencyApi()).RefreshAsync(CancellationToken.None);

        Assert.Equal(("VND", new DateOnly(2026, 10, 23), Today), Assert.Single(frankfurter.Requests));
        Assert.Equal(
            [
                (new DateOnly(2026, 10, 22), FxRatesRefresher.FrankfurterSource),
                (new DateOnly(2026, 10, 23), FxRatesRefresher.FrankfurterSource),
                (new DateOnly(2026, 10, 24), FxRatesRefresher.FrankfurterSource),
                (new DateOnly(2026, 10, 25), FxRatesRefresher.CurrencyApiSource),
                (Today, FxRatesRefresher.FrankfurterSource)
            ],
            (await db.ExchangeRates.OrderBy(r => r.Date).ToListAsync()).Select(r => (r.Date, r.Source)));
    }

    [Fact]
    public async Task ARefresh_EvictsTheCachedRates_SoAConversionSeesTheNewDay()
    {
        await using AppDbContext db = CreateContext();
        db.ExchangeRates.Add(Rate(new DateOnly(2026, 10, 23), 25_500m, FxRatesRefresher.FrankfurterSource));
        await db.SaveChangesAsync();
        ExchangeRateCache cache = TestCurrencyConverter.NewCache();
        ICurrencyConverter converter = TestCurrencyConverter.Create(db, cache, new FixedTimeProvider(Now));
        Assert.Equal(25_500m, (await converter.PrepareAsync("VND", [new MoneyAt("USD", null)])).Convert(1m, "USD", null));

        await Refresher(db, new FakeFrankfurter(), new FakeCurrencyApi(), cache).RefreshAsync(CancellationToken.None);

        decimal todaysRate = (await db.ExchangeRates.SingleAsync(r => r.Date == Today)).Rate;
        Assert.Equal(todaysRate, (await converter.PrepareAsync("VND", [new MoneyAt("USD", null)])).Convert(1m, "USD", null));
    }

    [Fact]
    public async Task WhenBothSourcesFail_TheRefreshThrows_AndStoresNothing()
    {
        await using AppDbContext db = CreateContext();
        FxRatesRefresher refresher = Refresher(db, new FakeFrankfurter { Failure = new InvalidDataException("bad") },
            new FakeCurrencyApi { Failure = new HttpRequestException("down") });

        await Assert.ThrowsAsync<HttpRequestException>(() => refresher.RefreshAsync(CancellationToken.None));

        Assert.Empty(await db.ExchangeRates.ToListAsync());
    }

    [Fact]
    public void Frankfurter_ParsesItsRows_AndRejectsAnotherPairOrAMissingRate()
    {
        List<FrankfurterClient.Row> rows =
        [
            new("2026-10-24", "USD", "VND", 25_843m),
            new("2026-10-25", "USD", "VND", 25_818m)
        ];

        Assert.Equal([new DailyRate(new DateOnly(2026, 10, 24), 25_843m), new DailyRate(new DateOnly(2026, 10, 25), 25_818m)],
            FrankfurterClient.Parse(rows, "VND"));
        Assert.Throws<InvalidDataException>(() => FrankfurterClient.Parse([new("2026-10-25", "EUR", "VND", 27_000m)], "VND"));
        Assert.Throws<InvalidDataException>(() => FrankfurterClient.Parse([new("2026-10-25", "USD", "VND", 0m)], "VND"));
        Assert.Throws<InvalidDataException>(() => FrankfurterClient.Parse([new("25/10/2026", "USD", "VND", 1m)], "VND"));
        Assert.Throws<InvalidDataException>(() => FrankfurterClient.Parse(null, "VND"));
    }

    [Fact]
    public async Task Frankfurter_AsksForTheUsdRatesOfTheDays()
    {
        Uri? asked = null;
        var client = new FrankfurterClient(new HttpClient(new DelegatingTestHandler((request, _) =>
        {
            asked = request.RequestUri;
            return Task.FromResult(Json("""[{"date":"2026-10-25","base":"USD","quote":"VND","rate":25818}]"""));
        })));

        IReadOnlyList<DailyRate> rates = await client.GetUsdRatesAsync("VND", new DateOnly(2026, 10, 25),
            new DateOnly(2026, 10, 25), CancellationToken.None);

        Assert.Equal("https://api.frankfurter.dev/v2/rates?base=USD&quotes=VND&from=2026-10-25&to=2026-10-25",
            asked?.ToString());
        Assert.Equal([new DailyRate(new DateOnly(2026, 10, 25), 25_818m)], rates);
    }

    [Fact]
    public void CurrencyApi_ReadsTheQuoteFromTheUsdTable()
    {
        using JsonDocument answer = JsonDocument.Parse("""{ "date": "2026-10-25", "usd": { "eur": 0.92, "vnd": 25831.81 } }""");
        using JsonDocument noVnd = JsonDocument.Parse("""{ "date": "2026-10-25", "usd": { "eur": 0.92 } }""");
        using JsonDocument noDate = JsonDocument.Parse("""{ "usd": { "vnd": 25831.81 } }""");

        Assert.Equal(new DailyRate(new DateOnly(2026, 10, 25), 25_831.81m), CurrencyApiClient.Parse(answer.RootElement, "VND"));
        Assert.Throws<InvalidDataException>(() => CurrencyApiClient.Parse(noVnd.RootElement, "VND"));
        Assert.Throws<InvalidDataException>(() => CurrencyApiClient.Parse(noDate.RootElement, "VND"));
    }

    [Fact]
    public async Task CurrencyApi_WhenJsDelivrFails_ReadsTheCloudflareMirror()
    {
        var asked = new List<string>();
        var client = new CurrencyApiClient(new HttpClient(new DelegatingTestHandler((request, _) =>
        {
            asked.Add(request.RequestUri!.Host);
            return Task.FromResult(asked.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json("""{ "date": "2026-10-25", "usd": { "vnd": 25831.81 } }"""));
        })));

        DailyRate rate = await client.GetLatestUsdRateAsync("VND", CancellationToken.None);

        Assert.Equal(["cdn.jsdelivr.net", "latest.currency-api.pages.dev"], asked);
        Assert.Equal(25_831.81m, rate.Rate);
    }

    private static FxRatesRefresher Refresher(AppDbContext db, IFrankfurterClient frankfurter,
        ICurrencyApiClient currencyApi, ExchangeRateCache? cache = null) =>
        new(db, frankfurter, currencyApi, cache ?? TestCurrencyConverter.NewCache(), new FixedTimeProvider(Now),
            NullLogger<FxRatesRefresher>.Instance);

    private static ExchangeRate Rate(DateOnly date, decimal rate, string source) =>
        new() { BaseCurrency = "USD", QuoteCurrency = "VND", Date = date, Rate = rate, Source = source };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FxRates_{Guid.NewGuid():N}")
            .Options);

    /// <summary>Answers one rate a day, 25,000 plus the day of the month, for the days asked.</summary>
    private sealed class FakeFrankfurter : IFrankfurterClient
    {
        public List<(string Quote, DateOnly From, DateOnly To)> Requests { get; } = [];
        public Exception? Failure { get; init; }
        public bool AnswersNothing { get; init; }

        public Task<IReadOnlyList<DailyRate>> GetUsdRatesAsync(string quoteCurrency, DateOnly from, DateOnly to,
            CancellationToken cancellationToken)
        {
            Requests.Add((quoteCurrency, from, to));
            if (Failure is not null)
            {
                return Task.FromException<IReadOnlyList<DailyRate>>(Failure);
            }

            IReadOnlyList<DailyRate> rates = AnswersNothing
                ? []
                : Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                    .Select(offset => from.AddDays(offset))
                    .Select(day => new DailyRate(day, 25_000m + day.Day))
                    .ToList();
            return Task.FromResult(rates);
        }
    }

    private sealed class FakeCurrencyApi : ICurrencyApiClient
    {
        public int Requests { get; private set; }
        public DailyRate Latest { get; init; } = new(Today, 25_831.81m);
        public Exception? Failure { get; init; }

        public Task<DailyRate> GetLatestUsdRateAsync(string quoteCurrency, CancellationToken cancellationToken)
        {
            Requests++;
            return Failure is null ? Task.FromResult(Latest) : Task.FromException<DailyRate>(Failure);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
