using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models.Budget;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// <see cref="MoneyRangeAttribute"/> on the Budget Planner and Net Worth forms (#444): the bound follows the row's
/// currency, a US dollar form keeps the dollar bound, and the message names the bound formatted in that currency and
/// the request culture.
/// </summary>
public class MoneyRangeAttributeTests
{
    private static List<ValidationResult> Validate(object model)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<IStringLocalizer>(TestLocalization.Localizer)
            .BuildServiceProvider();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, services, null), results, validateAllProperties: true);
        return results;
    }

    private static List<ValidationResult> ValidateIn(string language, string culture, object model)
    {
        using var uiCulture = new UiCultureScope(language);
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return Validate(model);
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    [Fact]
    public void Budget_ADongIncomeOverTheDollarBound_IsValid_AndTheSameFigureInDollarsIsNot()
    {
        var budget = new BudgetViewModel { CurrencyCode = "VND", MonthlyIncome = 30_000_000m, Housing = 20_000_000m };
        Assert.Empty(Validate(budget));

        budget.CurrencyCode = "USD";
        List<ValidationResult> results = ValidateIn("en", "en-US", budget);

        Assert.Equal(2, results.Count);
        ValidationResult income = Assert.Single(results, r => r.MemberNames.Contains(nameof(BudgetViewModel.MonthlyIncome)));
        Assert.Equal("Monthly income must be between $0 and $10,000,000.", income.ErrorMessage);
        ValidationResult housing = Assert.Single(results, r => r.MemberNames.Contains(nameof(BudgetViewModel.Housing)));
        Assert.Equal("Amount must be between $0 and $10,000,000.", housing.ErrorMessage);
    }

    [Fact]
    public void Budget_ADongAmountOverTheDongBound_IsRejected_WithTheDongBound()
    {
        var budget = new BudgetViewModel { CurrencyCode = "VND", MonthlyIncome = 250_000_000_001m };

        ValidationResult result = Assert.Single(ValidateIn("en", "en-US", budget));

        Assert.Equal("Monthly income must be between ₫0 and ₫250,000,000,000.", result.ErrorMessage);
    }

    [Fact]
    public void Budget_InVietnamese_FormatsTheBoundForTheCulture()
    {
        var budget = new BudgetViewModel { CurrencyCode = "VND", Food = -1m };

        ValidationResult result = Assert.Single(ValidateIn("vi", "vi-VN", budget));

        Assert.Contains("250.000.000.000", result.ErrorMessage);
        Assert.Contains("₫", result.ErrorMessage);
        Assert.DoesNotContain("{", result.ErrorMessage);
    }

    [Fact]
    public void NetWorth_DongAmountsOverTheDollarBounds_AreValid()
    {
        var model = new NetWorthViewModel
        {
            CurrencyCode = "VND", StartingBalance = 50_000_000_000m, AnnualSavings = 300_000_000m,
            AnnualIncome = 3_000_000_000m, AnnualSpending = 300_000_000m, AggressiveSavings = 300_000_000m
        };

        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData(null)]
    public void NetWorth_ADollarOrUnstatedCurrency_KeepsTheDollarBounds(string? currencyCode)
    {
        var model = new NetWorthViewModel { CurrencyCode = currencyCode, StartingBalance = 50_000_000_000m };

        ValidationResult result = Assert.Single(ValidateIn("en", "en-US", model));

        Assert.Contains(nameof(NetWorthViewModel.StartingBalance), result.MemberNames);
        Assert.Equal("Starting balance must be between $0 and $1,000,000,000.", result.ErrorMessage);
    }

    [Fact]
    public void NetWorth_ADongAmountOverTheDongBound_IsRejected()
    {
        var model = new NetWorthViewModel { CurrencyCode = "VND", AnnualIncome = 2_500_000_000_001m };

        ValidationResult result = Assert.Single(ValidateIn("en", "en-US", model));

        Assert.Equal("Annual income must be between ₫0 and ₫2,500,000,000,000.", result.ErrorMessage);
    }

    [Fact]
    public void WithoutALocalizer_TheKeyIsTheTemplate()
    {
        var budget = new BudgetViewModel { MonthlyIncome = -1m };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(budget, new ValidationContext(budget), results, validateAllProperties: true);

        Assert.Equal("BudgetPlanner.Validation.IncomeRange", Assert.Single(results).ErrorMessage);
    }
}
