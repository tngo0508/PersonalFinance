using System.Text.Json;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Shared;

/// <summary>
/// The <see cref="ConvertibleCurrencies"/> constant (ADR 0016): the currencies the rate refresh quotes and the converter
/// offers, wider than the <see cref="SupportedCurrencies"/> an amount may be in.
/// </summary>
public class ConvertibleCurrenciesTests
{
    [Fact]
    public void All_IsTheElevenCurrencies_InTheirOrder()
    {
        Assert.Equal(["USD", "VND", "JPY", "EUR", "GBP", "CNY", "KRW", "THB", "SGD", "AUD", "CAD"],
            ConvertibleCurrencies.All.Select(c => c.Code));
    }

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("VND", 0)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("EUR", 2)]
    [InlineData("GBP", 2)]
    [InlineData("CNY", 2)]
    [InlineData("THB", 2)]
    [InlineData("SGD", 2)]
    [InlineData("AUD", 2)]
    [InlineData("CAD", 2)]
    public void Find_ReturnsTheCurrency_WithItsIsoMinorUnits(string code, int minorUnits)
    {
        ConvertibleCurrency? currency = ConvertibleCurrencies.Find(code);

        Assert.NotNull(currency);
        Assert.Equal(code, currency.Code);
        Assert.Equal(minorUnits, currency.MinorUnits);
    }

    [Theory]
    [InlineData("CHF")]
    [InlineData("jpy")]
    [InlineData(" JPY")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_KnowsNothingButAnExactConvertibleCode(string? code)
    {
        Assert.Null(ConvertibleCurrencies.Find(code));
        Assert.False(ConvertibleCurrencies.IsConvertible(code));
    }

    [Theory]
    [InlineData("JPY", 1500.5, 1501)]
    [InlineData("JPY", -1500.5, -1501)]
    [InlineData("KRW", 1321.49, 1321)]
    [InlineData("EUR", 2.345, 2.35)]
    [InlineData("GBP", -2.345, -2.35)]
    [InlineData("CAD", 2.344, 2.34)]
    [InlineData("AUD", 0, 0)]
    public void Round_GoesToTheMinorUnits_HalvesAwayFromZero(string code, decimal amount, decimal expected)
    {
        Assert.Equal(expected, ConvertibleCurrencies.Round(code, amount));
        Assert.Equal(expected, ConvertibleCurrencies.Find(code)!.Round((decimal?)amount));
    }

    [Fact]
    public void Round_LeavesNoAmountAsNone()
    {
        Assert.Null(ConvertibleCurrencies.Find("JPY")!.Round((decimal?)null));
    }

    [Theory]
    [InlineData("CHF")]
    [InlineData("eur")]
    public void Round_InACurrencyThatIsNotConvertible_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => ConvertibleCurrencies.Round(code, 1m));
    }

    [Fact]
    public void EverySupportedCurrency_IsConvertible_WithTheSameMinorUnits()
    {
        Assert.All(SupportedCurrencies.All, supported =>
            Assert.Equal(supported.MinorUnits, ConvertibleCurrencies.Find(supported.Code)?.MinorUnits));
    }

    [Fact]
    public void EveryCode_HasTheFixedLength_AndAnEnglishName()
    {
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot.SourceProject("PersonalFinance.Web"), "Resources", "i18n", "en.json")));

        Assert.All(ConvertibleCurrencies.All, currency =>
        {
            Assert.Equal(SupportedCurrencies.CodeLength, currency.Code.Length);
            Assert.Equal($"Common.Currency.Name.{currency.Code}", currency.NameKey);
            Assert.True(english.RootElement.TryGetProperty(currency.NameKey, out JsonElement name),
                $"en.json has no {currency.NameKey}");
            Assert.False(string.IsNullOrWhiteSpace(name.GetString()));
        });
    }
}
