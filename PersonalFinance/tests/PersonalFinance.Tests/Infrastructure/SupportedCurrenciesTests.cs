using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>The <see cref="SupportedCurrencies"/> constant (ADR 0016): code, display name, minor units, symbol.</summary>
public class SupportedCurrenciesTests
{
    [Theory]
    [InlineData("USD", 2, "$")]
    [InlineData("VND", 0, "₫")]
    public void Find_ReturnsTheCurrency_WithItsMinorUnitsAndSymbol(string code, int minorUnits, string symbol)
    {
        SupportedCurrency? currency = SupportedCurrencies.Find(code);

        Assert.NotNull(currency);
        Assert.Equal(minorUnits, currency.MinorUnits);
        Assert.Equal(symbol, currency.Symbol);
        Assert.False(string.IsNullOrWhiteSpace(currency.DisplayName));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData(" USD")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSupported_RejectsAnythingButAnExactSupportedCode(string? code)
    {
        Assert.False(SupportedCurrencies.IsSupported(code));
    }

    [Fact]
    public void Default_IsUsd()
    {
        Assert.Equal("USD", SupportedCurrencies.Default.Code);
    }
}
