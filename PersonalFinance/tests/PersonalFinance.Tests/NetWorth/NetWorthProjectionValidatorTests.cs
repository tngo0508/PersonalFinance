using System.Globalization;
using PersonalFinance.ApiService.Features.NetWorth.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// <see cref="NetWorthProjectionValidator"/> amount bounds (#444): <see cref="NetWorthLimits"/> in US dollars, scaled
/// to the request's currency, with the bound carried as a money argument the Web formats in that currency.
/// </summary>
public class NetWorthProjectionValidatorTests
{
    private static NetWorthProjectionRequestDto Valid(string? currencyCode) => new()
    {
        CurrencyCode = currencyCode, StartingBalance = 1000m, AnnualSavings = 1000m, AnnualIncome = 50_000m,
        AnnualSpending = 30_000m, AggressiveSavings = 2000m, CurrentAge = 30, RetirementAge = 65, BaseRoi = 7m,
        ConservativeRoi = 4m, AggressiveRoi = 10m
    };

    [Fact]
    public void DongAmountsOverTheDollarBounds_AreAccepted()
    {
        NetWorthProjectionRequestDto request = Valid("VND");
        request.StartingBalance = 50_000_000_000m;
        request.AnnualSavings = 300_000_000m;
        request.AnnualIncome = 3_000_000_000m;
        request.AnnualSpending = 300_000_000m;
        request.AggressiveSavings = 300_000_000m;

        Assert.Null(NetWorthProjectionValidator.Validate(request));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData(null)]
    public void TheSameFiguresInDollars_AreStillRejected_WithTheDollarBound(string? currencyCode)
    {
        NetWorthProjectionRequestDto request = Valid(currencyCode);
        request.AnnualSavings = 300_000_000m;

        ApiErrorEntryDto failure = NetWorthProjectionValidator.Validate(request)!;

        Assert.Equal(ApiErrorKeys.NetWorth.AnnualSavingsRange, failure.Key);
        Assert.Equal(nameof(request.AnnualSavings), failure.Field);
        Assert.Equal("Annual savings must be between $0 and $10,000,000.", failure.Message);
        Assert.Equal("10000000", failure.Args!["max"].Value);
        Assert.Equal("USD", failure.Args["max"].Currency);
    }

    [Fact]
    public void ADongAmountOverTheDongBound_IsRejected_WithTheDongBoundAsAMoneyArgument()
    {
        NetWorthProjectionRequestDto request = Valid("VND");
        request.StartingBalance = 25_000_000_000_001m;

        ApiErrorEntryDto failure = NetWorthProjectionValidator.Validate(request)!;

        Assert.Equal(ApiErrorKeys.NetWorth.StartingBalanceRange, failure.Key);
        Assert.Equal(nameof(request.StartingBalance), failure.Field);
        Assert.Equal("Starting balance must be between ₫0 and ₫25,000,000,000,000.", failure.Message);
        Assert.Equal(ApiErrorArgKind.Money, failure.Args!["max"].Kind);
        Assert.Equal("25000000000000", failure.Args["max"].Value);
        Assert.Equal("VND", failure.Args["max"].Currency);
        Assert.Equal("0", failure.Args["min"].Value);
        Assert.Equal("VND", failure.Args["min"].Currency);
    }

    [Fact]
    public void ANegativeAmount_IsRejected_WhateverTheCurrency()
    {
        NetWorthProjectionRequestDto request = Valid("VND");
        request.AnnualSpending = -1m;

        Assert.Equal(ApiErrorKeys.NetWorth.AnnualSpendingRange, NetWorthProjectionValidator.Validate(request)?.Key);
    }

    [Fact]
    public void TheRangeMessage_ResolvesWithTheBoundFormattedInTheCurrency()
    {
        NetWorthProjectionRequestDto request = Valid("VND");
        request.AnnualIncome = 2_500_000_000_001m;
        ApiErrorEntryDto failure = NetWorthProjectionValidator.Validate(request)!;

        using var uiCulture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Equal("Annual income must be between ₫0 and ₫2,500,000,000,000.",
                ApiErrorMessages.Resolve(TestLocalization.Localizer, failure));
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(20.1)]
    public void InflationOutsideZeroToTwentyPercent_IsRejected(double inflation)
    {
        NetWorthProjectionRequestDto request = Valid("USD");
        request.InflationPercent = (decimal)inflation;

        Assert.Equal(ApiErrorKeys.NetWorth.InflationRange, NetWorthProjectionValidator.Validate(request)?.Key);
    }
}
