using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.CurrencyConverter;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.CurrencyConverter;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.CurrencyConversion;

/// <summary>
/// The Currency Converter's calculation (#446) through the shared converter: the cross rate goes through USD at full
/// precision; the amount is rounded once to the To currency's minor units, halves away from zero; From = To needs no
/// rate; the latest uses each leg's newest rate and a picked date each leg's nearest prior one; the rate date is the
/// older leg's, stale when more than three days before the day asked for; a leg with no rate is unavailable, never zero.
/// </summary>
public class CurrencyConversionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CrossRate_GoesThroughUsd()
    {
        await using AppDbContext db = CreateContext(Rate("JPY", 26, 150m), Rate("VND", 26, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1_000_000m, "JPY", "VND");

        Assert.Equal(170m, result.Rate);
        Assert.Equal(170_000_000m, result.ConvertedAmount);
        Assert.Equal(new DateOnly(2026, 10, 26), result.RateDate);
        Assert.Equal(["frankfurter"], result.Sources);
        Assert.False(result.IsStale);
        Assert.False(result.Unavailable);
    }

    [Fact]
    public async Task CrossRate_IsNotRoundedOnTheWay()
    {
        await using AppDbContext db = CreateContext(Rate("JPY", 26, 140m), Rate("VND", 26, 25_000m));

        CurrencyConversionDto result = await ConvertAsync(db, 1_000_000m, "JPY", "VND");

        Assert.Equal(25_000m / 140m, result.Rate);
        Assert.Equal(178_571_429m, result.ConvertedAmount);
    }

    [Theory]
    [InlineData("USD", "EUR", "EUR", "0.5", "1.01", "0.51")]
    [InlineData("EUR", "USD", "EUR", "2", "1.01", "0.51")]
    [InlineData("USD", "JPY", "JPY", "100", "0.025", "3")]
    [InlineData("USD", "VND", "VND", "100", "0.025", "3")]
    [InlineData("USD", "KRW", "KRW", "100", "0.045", "5")]
    public async Task ConvertedAmount_IsRoundedOnceToTheToCurrencysMinorUnits_HalvesAwayFromZero(string from, string to,
        string quote, string rate, string amount, string expected)
    {
        await using AppDbContext db = CreateContext(Rate(quote, 26, decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)));

        CurrencyConversionDto result = await ConvertAsync(db,
            decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), from, to);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.ConvertedAmount);
    }

    [Fact]
    public async Task FromEqualsTo_ReturnsTheAmountUnchanged_AtRateOne_WithNoLookup()
    {
        await using AppDbContext db = CreateContext();

        CurrencyConversionDto result = await ConvertAsync(db, 1234.567m, "JPY", "JPY", new DateOnly(2020, 1, 1));

        Assert.Equal(1234.567m, result.ConvertedAmount);
        Assert.Equal(1m, result.Rate);
        Assert.Null(result.RateDate);
        Assert.Empty(result.Sources);
        Assert.False(result.IsStale);
        Assert.False(result.Unavailable);
    }

    [Fact]
    public async Task NoDate_UsesEachLegsNewestRate_AndTheOlderLegsDay()
    {
        await using AppDbContext db = CreateContext(
            Rate("JPY", 20, 140m), Rate("JPY", 24, 150m), Rate("VND", 22, 25_000m), Rate("VND", 26, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1m, "JPY", "VND");

        Assert.Equal(170m, result.Rate);
        Assert.Equal(new DateOnly(2026, 10, 24), result.RateDate);
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task PickedDate_UsesEachLegsNearestPriorRate_AndTheOlderLegsDay()
    {
        await using AppDbContext db = CreateContext(
            Rate("JPY", 20, 140m), Rate("JPY", 24, 150m), Rate("VND", 22, 25_200m), Rate("VND", 26, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1m, "JPY", "VND", new DateOnly(2026, 10, 25));

        Assert.Equal(168m, result.Rate);
        Assert.Equal(new DateOnly(2026, 10, 22), result.RateDate);
        Assert.False(result.IsStale);
    }

    [Theory]
    [InlineData(23, false)]
    [InlineData(24, true)]
    public async Task PickedDate_IsStale_WhenTheRateDateIsMoreThanThreeDaysBefore(int pickedDay, bool stale)
    {
        await using AppDbContext db = CreateContext(Rate("JPY", 20, 150m), Rate("VND", 20, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1m, "JPY", "VND", new DateOnly(2026, 10, pickedDay));

        Assert.Equal(new DateOnly(2026, 10, 20), result.RateDate);
        Assert.Equal(stale, result.IsStale);
    }

    [Fact]
    public async Task NoDate_IsStale_WhenTheNewestRateIsMoreThanThreeDaysBeforeToday()
    {
        await using AppDbContext db = CreateContext(Rate("JPY", 22, 150m), Rate("VND", 26, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1m, "JPY", "VND");

        Assert.Equal(new DateOnly(2026, 10, 22), result.RateDate);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task ALegWithNoRate_IsUnavailable_WithNoAmount()
    {
        await using AppDbContext db = CreateContext(Rate("VND", 26, 25_500m));

        CurrencyConversionDto result = await ConvertAsync(db, 1_000_000m, "JPY", "VND");

        Assert.True(result.Unavailable);
        Assert.Null(result.ConvertedAmount);
        Assert.Null(result.Rate);
        Assert.Null(result.RateDate);
    }

    [Fact]
    public async Task APickedDateBeforeEveryRate_IsUnavailable()
    {
        await using AppDbContext db = CreateContext(Rate("EUR", 26, 0.9m));

        CurrencyConversionDto result = await ConvertAsync(db, 10m, "USD", "EUR", new DateOnly(2026, 10, 1));

        Assert.True(result.Unavailable);
        Assert.Null(result.ConvertedAmount);
    }

    [Fact]
    public async Task Sources_NameEveryLegsSource_Once()
    {
        await using AppDbContext db = CreateContext(
            Rate("JPY", 26, 150m, "currency-api"), Rate("VND", 26, 25_500m), Rate("EUR", 26, 0.9m));

        CurrencyConversionDto mixed = await ConvertAsync(db, 1m, "JPY", "VND");
        CurrencyConversionDto single = await ConvertAsync(db, 1m, "USD", "EUR");

        Assert.Equal(["currency-api", "frankfurter"], mixed.Sources);
        Assert.Equal(["frankfurter"], single.Sources);
    }

    [Fact]
    public async Task SharedConverter_PreparesAnyConvertibleTarget_AndRejectsOthers()
    {
        await using AppDbContext db = CreateContext(Rate("JPY", 26, 150m));
        ICurrencyConverter converter = TestCurrencyConverter.Create(db, new FixedTimeProvider(Now));

        ReportingConversion conversion = await converter.PrepareAsync("JPY", [new MoneyAt("USD", null)]);

        Assert.Equal(150m, conversion.Convert(1m, "USD", null));
        Assert.Equal(2m, conversion.Round(1.5m));
        await Assert.ThrowsAsync<ArgumentException>(() => converter.PrepareAsync("XYZ", [new MoneyAt("USD", null)]));
    }

    private static Task<CurrencyConversionDto> ConvertAsync(AppDbContext db, decimal amount, string from, string to,
        DateOnly? date = null)
    {
        var service = new CurrencyConversionService(TestCurrencyConverter.Create(db, new FixedTimeProvider(Now)));
        return service.ConvertAsync(new ConvertCurrencyRequestDto { Amount = amount, From = from, To = to, Date = date });
    }

    private static AppDbContext CreateContext(params ExchangeRate[] rates)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CurrencyConversion_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.ExchangeRates.AddRange(rates);
        db.SaveChanges();
        return db;
    }

    private static ExchangeRate Rate(string quote, int octoberDay, decimal rate, string source = "frankfurter") => new()
    {
        BaseCurrency = "USD",
        QuoteCurrency = quote,
        Date = new DateOnly(2026, 10, octoberDay),
        Rate = rate,
        Source = source
    };
}
