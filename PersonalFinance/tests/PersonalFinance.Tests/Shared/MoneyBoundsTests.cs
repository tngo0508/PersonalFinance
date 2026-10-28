using System.Globalization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Tests.Shared;

/// <summary>
/// <see cref="MoneyBounds"/> (#444): a bound written in US dollars is multiplied by the currency's
/// <see cref="SupportedCurrency.BoundScale"/>, a missing or unsupported code reads as US dollars, and the English
/// fallback text names the bound in the currency.
/// </summary>
public class MoneyBoundsTests
{
    [Theory]
    [InlineData("USD", 10_000_000)]
    [InlineData("VND", 250_000_000_000)]
    [InlineData(null, 10_000_000)]
    [InlineData("EUR", 10_000_000)]
    public void Max_ScalesADollarBoundToTheCurrency(string? currencyCode, decimal expected)
    {
        Assert.Equal(expected, MoneyBounds.Max(BudgetPlannerLimits.MaxAmount, currencyCode));
    }

    [Fact]
    public void EveryCurrency_HasAScale_AndDollarsAreNotScaled()
    {
        Assert.Equal(1, SupportedCurrencies.Usd.BoundScale);
        Assert.All(SupportedCurrencies.All, currency => Assert.True(currency.BoundScale >= 1, currency.Code));
    }

    [Fact]
    public void MaxInvariant_IgnoresTheRequestCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            Assert.Equal("250000000000", MoneyBounds.MaxInvariant(BudgetPlannerLimits.MaxAmount, "VND"));
            Assert.Equal("1000000000", MoneyBounds.MaxInvariant(NetWorthLimits.StartingBalanceMax, "USD"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("USD", "Enter a monthly amount over $0 and at most $1,000,000.")]
    [InlineData("VND", "Enter a monthly amount over ₫0 and at most ₫25,000,000,000.")]
    [InlineData(null, "Enter a monthly amount over $0 and at most $1,000,000.")]
    public void Describe_FillsTheBoundsInTheCurrency(string? currencyCode, string expected)
    {
        Assert.Equal(expected, MoneyBounds.Describe(MoneyPrioritiesMessages.MonthlyAmountRange,
            MoneyPrioritiesLimits.MonthlyAmountMax, currencyCode));
    }
}
