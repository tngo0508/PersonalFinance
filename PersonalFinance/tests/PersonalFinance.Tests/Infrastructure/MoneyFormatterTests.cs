using System.Globalization;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The shared money formatter (localization spec §2.2, §5): the symbol and minor units come from the currency, the
/// separators from the culture. Never the culture's own currency symbol.
/// </summary>
public class MoneyFormatterTests
{
    private const char Nbsp = ' ';
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo ViVn = CultureInfo.GetCultureInfo("vi-VN");

    [Fact]
    public void Usd_InEnUs_HasDollarSignAndCents()
    {
        Assert.Equal("$1,234.56", MoneyFormatter.Format(1234.56m, "USD", EnUs));
    }

    [Fact]
    public void Vnd_InViVn_HasNoMinorUnitsAndDongSign()
    {
        Assert.Equal($"1.234.567{Nbsp}₫", MoneyFormatter.Format(1234567m, "VND", ViVn));
    }

    [Fact]
    public void Usd_InViVn_KeepsTheDollarSign_WithVietnameseSeparators()
    {
        Assert.Equal($"1.234,56{Nbsp}$", MoneyFormatter.Format(1234.56m, "USD", ViVn));
    }

    [Fact]
    public void Vnd_InEnUs_KeepsTheDongSign_WithEnglishSeparators()
    {
        Assert.Equal("₫1,234,567", MoneyFormatter.Format(1234567m, "VND", EnUs));
    }

    [Theory]
    [InlineData("en-US", "USD", "-$1,234.56")]
    [InlineData("vi-VN", "USD", "-1.234,56 $")]
    [InlineData("en-US", "VND", "-₫1,234,567")]
    [InlineData("vi-VN", "VND", "-1.234.567 ₫")]
    public void Negative_HasALeadingMinus(string culture, string currency, string expected)
    {
        decimal amount = currency == "USD" ? -1234.56m : -1234567m;

        Assert.Equal(expected, MoneyFormatter.Format(amount, currency, CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData("en-US", "USD", "$0.00")]
    [InlineData("vi-VN", "USD", "0,00 $")]
    [InlineData("en-US", "VND", "₫0")]
    [InlineData("vi-VN", "VND", "0 ₫")]
    public void Zero_HasNoSign(string culture, string currency, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format(0m, currency, CultureInfo.GetCultureInfo(culture)));
        Assert.Equal(expected, MoneyFormatter.Format(-0.004m, currency, CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData(2.345, "$2.35")]
    [InlineData(2.355, "$2.36")]
    [InlineData(-2.345, "-$2.35")]
    [InlineData(0.005, "$0.01")]
    public void Usd_RoundsHalfAwayFromZero_ToCents(decimal amount, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format(amount, "USD", EnUs));
    }

    [Theory]
    [InlineData(1234.5, "1.235 ₫")]
    [InlineData(1234.49, "1.234 ₫")]
    [InlineData(-1234.5, "-1.235 ₫")]
    public void Vnd_RoundsHalfAwayFromZero_ToWholeDong(decimal amount, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format(amount, "VND", ViVn));
    }

    [Theory]
    [InlineData("en-US", "$1,235")]
    [InlineData("vi-VN", "1.235 $")]
    public void FormatWhole_DropsTheCents_RoundingHalfAwayFromZero(string culture, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.FormatWhole(1234.5m, "USD", CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void WithoutACulture_UsesTheCurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = ViVn;
            Assert.Equal($"1.234,56{Nbsp}$", MoneyFormatter.Format(1234.56m, "USD"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData("")]
    public void UnsupportedCurrency_Throws(string currency)
    {
        Assert.Throws<ArgumentException>(() => MoneyFormatter.Format(1m, currency, EnUs));
    }
}
