using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The stored Reporting Currency (ADR 0016, localization spec §2.5): US dollars until chosen, one row per Account
/// beside the Language, only a supported currency, and never a rewrite of a stored amount (the service touches only
/// the preference row).
/// </summary>
public class ReportingCurrencyPreferenceTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportingCurrency_NeverChosen_ReadsAsUsd()
    {
        await using AppDbContext db = CreateContext();

        Assert.Equal("USD", await new UserPreferenceService(db, new FixedTimeProvider(Now)).GetReportingCurrencyAsync(UserId));
    }

    [Fact]
    public async Task ReportingCurrency_SetOnTheLanguageRow_KeepsOneRow_AndLeavesTheLanguage()
    {
        await using AppDbContext db = CreateContext();
        var service = new UserPreferenceService(db, new FixedTimeProvider(Now));
        await service.SetLanguageAsync(UserId, "vi");

        await service.SetReportingCurrencyAsync(UserId, "VND");
        await service.SetReportingCurrencyAsync(UserId, "USD");
        await service.SetReportingCurrencyAsync(UserId, "VND");

        UserPreference row = Assert.Single(await db.UserPreferences.ToListAsync());
        Assert.Equal(("vi", "VND"), (row.Language, row.ReportingCurrency));
        Assert.Equal("VND", await service.GetReportingCurrencyAsync(UserId));
        Assert.Equal("vi", await service.GetLanguageAsync(UserId));
    }

    [Fact]
    public async Task ReportingCurrency_SetBeforeAnyLanguage_LeavesTheLanguageUnchosen()
    {
        await using AppDbContext db = CreateContext();
        var service = new UserPreferenceService(db, new FixedTimeProvider(Now));

        await service.SetReportingCurrencyAsync(UserId, "VND");

        Assert.Equal("VND", await service.GetReportingCurrencyAsync(UserId));
        Assert.Null(await service.GetLanguageAsync(UserId));
    }

    [Fact]
    public async Task ReportingCurrency_Unsupported_IsRejected_AndStoresNothing()
    {
        await using AppDbContext db = CreateContext();
        var service = new UserPreferenceService(db, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetReportingCurrencyAsync(UserId, "EUR"));

        Assert.Empty(await db.UserPreferences.ToListAsync());
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportingCurrency_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
