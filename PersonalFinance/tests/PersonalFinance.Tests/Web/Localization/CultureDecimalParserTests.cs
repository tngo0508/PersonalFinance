using System.Globalization;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>What a money input shows (<see cref="MoneyFormatter.FormatAmount"/>) binds back to the same amount.</summary>
public class CultureDecimalParserTests
{
    [Theory]
    [InlineData("en-US", "USD", 0)]
    [InlineData("en-US", "USD", 1234567.89)]
    [InlineData("en-US", "USD", -0.5)]
    [InlineData("vi-VN", "USD", 1234567.89)]
    [InlineData("vi-VN", "USD", -1234.5)]
    [InlineData("vi-VN", "VND", 1234567)]
    [InlineData("en-US", "VND", 999)]
    public void FormattedAmount_RoundTrips(string cultureName, string currency, decimal amount)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        string shown = MoneyFormatter.FormatAmount(amount, currency, culture);

        Assert.True(CultureDecimalParser.TryParse(shown, culture, out decimal parsed), shown);
        Assert.Equal(amount, parsed);
    }
}
