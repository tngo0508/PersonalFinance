using System.Globalization;
using System.Text.RegularExpressions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;

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
    public void TheBrowserCopy_InCoreI18nJs_MatchesEveryCurrency()
    {
        // PFI18n.formatMoney keeps its own table; the browser and the server must render an amount the same way.
        string script = File.ReadAllText(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"),
            "wwwroot", "js", "core", "i18n.js"));
        var entries = Regex.Matches(script, @"^\s*([A-Z]{3}): \{ minorUnits: (\d+), symbol: '([^']+)' \}",
                RegexOptions.Multiline, TimeSpan.FromSeconds(1))
            .Select(match => (Code: match.Groups[1].Value, MinorUnits: int.Parse(match.Groups[2].Value,
                CultureInfo.InvariantCulture), Symbol: match.Groups[3].Value))
            .ToList();

        Assert.Equal(SupportedCurrencies.All.Select(c => (c.Code, c.MinorUnits, c.Symbol)), entries);
    }

    [Fact]
    public void Default_IsUsd()
    {
        Assert.Equal("USD", SupportedCurrencies.Default.Code);
    }
}
