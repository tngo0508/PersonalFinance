using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The currency converter (ADR 0016, localization spec §2.6 and §5): a dated amount uses its own day's rate and an
/// undated one the latest; a day without a rate uses the nearest prior one, flagged when it is more than three days
/// older; no rate at all is "unavailable" and never zero; an amount already in the Reporting Currency needs no rate and
/// no caption; a total is rounded once; USD is the base and the inverse is computed; cached rates stay until the
/// refresh evicts them.
/// </summary>
public class CurrencyConverterTests
{
    private const string UserId = "converter-user";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DatedAmount_UsesItsOwnDaysRate_AndAnUndatedOne_TheLatest()
    {
        await using AppDbContext db = CreateContext(Rate(23, 25_500m), Rate(26, 25_800m));
        var day = new DateOnly(2026, 10, 23);
        ReportingConversion conversion = await Converter(db)
            .PrepareAsync("USD", [new MoneyAt("VND", day), new MoneyAt("VND", null)]);

        Assert.Equal(10m, conversion.Convert(255_000m, "VND", day));
        Assert.Equal(10m, conversion.Convert(258_000m, "VND", null));
        ConversionDto dto = conversion.ToDto();
        Assert.True(dto.Converted);
        Assert.False(dto.Unavailable);
        ConversionRateDto rate = Assert.Single(dto.Rates);
        Assert.Equal(("USD", "VND", 25_800m, new DateOnly(2026, 10, 26), false, true),
            (rate.BaseCurrency, rate.QuoteCurrency, rate.Rate, rate.Date, rate.IsStale, rate.IsDated));
    }

    [Fact]
    public async Task DayWithoutARate_UsesTheNearestPriorOne_NotFlaggedWithinThreeDays()
    {
        await using AppDbContext db = CreateContext(Rate(20, 25_000m), Rate(23, 25_500m));
        var day = new DateOnly(2026, 10, 25);
        ReportingConversion conversion = await Converter(db).PrepareAsync("USD", [new MoneyAt("VND", day)]);

        Assert.Equal(2m, conversion.Convert(51_000m, "VND", day));
        ConversionRateDto rate = Assert.Single(conversion.ToDto().Rates);
        Assert.Equal(new DateOnly(2026, 10, 23), rate.Date);
        Assert.False(rate.IsStale);
    }

    [Fact]
    public async Task RateMoreThanThreeDaysOlderThanItsDay_IsFlaggedStale()
    {
        await using AppDbContext db = CreateContext(Rate(20, 25_000m));
        var day = new DateOnly(2026, 10, 24);
        ReportingConversion conversion = await Converter(db)
            .PrepareAsync("USD", [new MoneyAt("VND", day), new MoneyAt("VND", null)]);

        Assert.Equal(1m, conversion.Convert(25_000m, "VND", day));
        Assert.Equal(1m, conversion.Convert(25_000m, "VND", null));
        ConversionRateDto rate = Assert.Single(conversion.ToDto().Rates);
        Assert.True(rate.IsStale);
        Assert.Equal(new DateOnly(2026, 10, 20), rate.Date);
    }

    [Fact]
    public async Task NoRateAtAll_ConvertsToNull_KeepsTheOriginal_AndSaysUnavailable()
    {
        await using AppDbContext db = CreateContext();
        ReportingConversion conversion = await Converter(db).PrepareAsync("USD", [new MoneyAt("VND", null)]);

        Assert.Null(conversion.Convert(25_000m, "VND", null));
        Assert.Equal(25_000m, conversion.ConvertOrOriginal(25_000m, "VND", null));
        ConversionDto dto = conversion.ToDto();
        Assert.True(dto.Unavailable);
        Assert.True(dto.Converted);
        Assert.Empty(dto.Rates);
    }

    [Fact]
    public async Task AnAmountInTheReportingCurrency_NeedsNoRate_AndNoCaption()
    {
        await using AppDbContext db = CreateContext();
        ReportingConversion conversion = await Converter(db)
            .PrepareAsync("USD", [new MoneyAt("USD", null), new MoneyAt("USD", new DateOnly(2026, 10, 1))]);

        Assert.Equal(12.34m, conversion.Convert(12.34m, "USD", null));
        Assert.Equal(12.34m, conversion.Convert(12.34m, "USD", new DateOnly(2026, 10, 1)));
        ConversionDto dto = conversion.ToDto();
        Assert.False(dto.Converted);
        Assert.False(dto.Unavailable);
        Assert.Empty(dto.Rates);
    }

    [Fact]
    public async Task ReportingInVnd_MultipliesAUsdAmountByTheRate_AndRoundsToWholeDong()
    {
        await using AppDbContext db = CreateContext(Rate(26, 25_800m));
        ReportingConversion conversion = await Converter(db).PrepareAsync("VND", [new MoneyAt("USD", null)]);

        Assert.Equal(258_000m, conversion.Convert(10m, "USD", null));
        Assert.Equal(258_000m, conversion.Round(258_000.4m));
        Assert.Equal("VND", conversion.ReportingCurrency);
    }

    [Fact]
    public async Task Total_RoundsOnce_NotPerLine()
    {
        await using AppDbContext db = CreateContext(Rate(26, 25_800m));
        ReportingConversion conversion = await Converter(db).PrepareAsync("USD", [new MoneyAt("VND", null)]);
        decimal[] lines = [10_000m, 10_000m, 10_000m];

        List<decimal> converted = lines.Select(line => conversion.Convert(line, "VND", null)!.Value).ToList();

        Assert.Equal(1.16m, conversion.Total(converted));
        Assert.Equal(1.17m, converted.Sum(c => conversion.Round(c)));
    }

    [Fact]
    public async Task AnAmountNotPrepared_Throws_RatherThanConvertingSilently()
    {
        await using AppDbContext db = CreateContext(Rate(26, 25_800m));
        ReportingConversion conversion = await Converter(db).PrepareAsync("USD", [new MoneyAt("VND", null)]);

        Assert.Throws<InvalidOperationException>(() => conversion.Convert(1m, "VND", new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public async Task ReportingCurrency_IsTheStoredPreference_OrUsd()
    {
        await using AppDbContext db = CreateContext();
        db.UserPreferences.Add(new UserPreference { UserId = UserId, Language = "vi", ReportingCurrency = "VND" });
        await db.SaveChangesAsync();
        ICurrencyConverter converter = Converter(db);

        Assert.Equal("VND", await converter.GetReportingCurrencyAsync(UserId));
        Assert.Equal("USD", await converter.GetReportingCurrencyAsync("someone-else"));
    }

    [Fact]
    public async Task CachedRates_AreReusedUntilTheRefreshEvictsThem()
    {
        await using AppDbContext db = CreateContext(Rate(23, 25_500m));
        ExchangeRateCache cache = TestCurrencyConverter.NewCache();
        ICurrencyConverter converter = TestCurrencyConverter.Create(db, cache, new FixedTimeProvider(Now));
        Assert.Equal(25_500m, await LatestUsdInVndAsync(converter));

        db.ExchangeRates.Add(Rate(26, 25_800m));
        await db.SaveChangesAsync();
        Assert.Equal(25_500m, await LatestUsdInVndAsync(converter));

        cache.Evict("VND");
        Assert.Equal(25_800m, await LatestUsdInVndAsync(converter));
    }

    private static async Task<decimal?> LatestUsdInVndAsync(ICurrencyConverter converter) =>
        (await converter.PrepareAsync("VND", [new MoneyAt("USD", null)])).Convert(1m, "USD", null);

    private static ICurrencyConverter Converter(AppDbContext db) =>
        TestCurrencyConverter.Create(db, new FixedTimeProvider(Now));

    private static AppDbContext CreateContext(params ExchangeRate[] rates)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CurrencyConverter_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.ExchangeRates.AddRange(rates);
        db.SaveChanges();
        return db;
    }

    private static ExchangeRate Rate(int octoberDay, decimal rate) => new()
    {
        BaseCurrency = "USD",
        QuoteCurrency = "VND",
        Date = new DateOnly(2026, 10, octoberDay),
        Rate = rate,
        Source = "test"
    };
}
