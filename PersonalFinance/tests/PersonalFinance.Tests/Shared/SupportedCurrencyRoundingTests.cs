using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.Shared;

/// <summary>
/// How an amount is stored (localization spec §2.5): rounded to its currency's minor units, halves away from zero, so
/// a VND amount is whole đồng; and which currency a write stores when its request names none.
/// </summary>
public class SupportedCurrencyRoundingTests
{
    [Theory]
    [InlineData("USD", 1.005, 1.01)]
    [InlineData("USD", -1.005, -1.01)]
    [InlineData("USD", 1.004, 1.00)]
    [InlineData("USD", 0, 0)]
    [InlineData("VND", 125000.5, 125001)]
    [InlineData("VND", -125000.5, -125001)]
    [InlineData("VND", 125000.49, 125000)]
    public void Round_GoesToTheMinorUnits_HalvesAwayFromZero(string code, decimal amount, decimal expected)
    {
        Assert.Equal(expected, SupportedCurrencies.Round(code, amount));
        Assert.Equal(expected, SupportedCurrencies.Find(code)!.Round((decimal?)amount));
    }

    [Fact]
    public void Round_LeavesNoAmountAsNone()
    {
        Assert.Null(SupportedCurrencies.Vnd.Round((decimal?)null));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("vnd")]
    public void Round_InACurrencyThatIsNotSupported_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => SupportedCurrencies.Round(code, 1m));
    }

    [Theory]
    [InlineData("VND", "USD", "VND")]
    [InlineData(null, "VND", "VND")]
    [InlineData(null, null, "USD")]
    public void Resolve_TakesTheRequestedCode_ThenTheStoredOne_ThenUsd(string? requested, string? stored, string expected)
    {
        Assert.Equal(expected, CurrencyCodeRules.Resolve(requested, stored));
    }

    [Fact]
    public void TheDefaultCode_IsTheDefaultCurrencysCode_AndEveryCodeHasTheFixedLength()
    {
        Assert.Equal(SupportedCurrencies.DefaultCode, SupportedCurrencies.Default.Code);
        Assert.All(SupportedCurrencies.All, currency => Assert.Equal(SupportedCurrencies.CodeLength, currency.Code.Length));
    }
}
