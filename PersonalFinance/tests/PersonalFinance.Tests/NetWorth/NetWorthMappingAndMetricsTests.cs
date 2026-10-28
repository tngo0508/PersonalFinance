using System.Globalization;
using System.Reflection;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The Net Worth parameters are mapped by hand (ADR 0001); these tests make sure every parameter survives each
/// mapping, so adding a field in one place and forgetting another fails here.
/// </summary>
public class NetWorthMappingAndMetricsTests
{
    // Inflation only steers the page's today's-money view, so it is sent with each calculation but never saved.
    private static readonly string[] ParameterNames = typeof(NetWorthProjectionRequestDto)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => p.Name)
        .Where(name => name != nameof(NetWorthProjectionRequestDto.InflationPercent))
        .ToArray();

    [Fact]
    public void SavedParameters_MapToProjectionRequestAndDto_WithEveryParameter()
    {
        NetWorthParameters saved = WithDistinctValues(new NetWorthParameters());

        AssertSameParameters(saved, saved.ToProjectionRequest());
        AssertSameParameters(saved, saved.ToDto());
    }

    [Fact]
    public void SubmittedValues_AreAppliedToSavedParameters()
    {
        NetWorthProjectionRequestDto submitted = WithDistinctValues(new NetWorthProjectionRequestDto());
        var saved = new NetWorthParameters();

        saved.Apply(submitted);

        AssertSameParameters(submitted, saved);
    }

    [Fact]
    public void WebViewModel_RoundTripsEveryParameter()
    {
        NetWorthParametersDto saved = WithDistinctValues(new NetWorthParametersDto());
        var model = new NetWorthViewModel();

        model.ApplySaved(saved);

        AssertSameParameters(saved, model.ToProjectionRequest());
    }

    [Fact]
    public void WebViewModel_StartsFromNetWorthDefaults()
    {
        AssertSameParameters(NetWorthDefaults.CreateRequest(), new NetWorthViewModel().ToProjectionRequest());
    }

    [Fact]
    public void WebViewModel_SendsItsInflation_StartingFromTheDefault()
    {
        Assert.Equal(NetWorthDefaults.InflationPercent, new NetWorthViewModel().ToProjectionRequest().InflationPercent);
        Assert.Equal(4.5m, new NetWorthViewModel { InflationPercent = 4.5m }.ToProjectionRequest().InflationPercent);
    }

    [Theory]
    [InlineData(32000, 108409.60, 29.5)]
    [InlineData(10000, 30000, 33.3)]
    [InlineData(0, 50000, 0)]
    public void SavingsRate_IsPlannedSavingsOverIncome_RoundedToOneDecimal(decimal savings, decimal income,
        decimal expected)
    {
        Assert.Equal(expected, FinanceMetrics.SavingsRatePercent(savings, income));
    }

    [Fact]
    public void SavingsRate_WithoutIncome_IsUnknown()
    {
        Assert.Null(FinanceMetrics.SavingsRatePercent(1000m, 0m));
    }

    [Theory]
    [InlineData(3000, 4000, -33.3)]
    [InlineData(5200, 3100, 40.4)]
    [InlineData(0, 100, 0)]
    public void ActualSavingsRate_IsIncomeLeftAfterExpenses_RoundedToOneDecimal(decimal income, decimal expenses,
        decimal expected)
    {
        Assert.Equal(expected, FinanceMetrics.ActualSavingsRatePercent(income, expenses));
    }

    private static T WithDistinctValues<T>(T target)
    {
        int i = 1;
        foreach (string name in ParameterNames)
        {
            PropertyInfo property = typeof(T).GetProperty(name)!;
            property.SetValue(target, property.PropertyType == typeof(int) ? 20 + i
                : property.PropertyType == typeof(string) ? SupportedCurrencies.Vnd.Code
                : Convert.ChangeType(1000m + i, property.PropertyType, CultureInfo.InvariantCulture));
            i++;
        }

        return target;
    }

    private static void AssertSameParameters(object expected, object actual)
    {
        foreach (string name in ParameterNames)
        {
            Assert.True(Equals(expected.GetType().GetProperty(name)!.GetValue(expected),
                    actual.GetType().GetProperty(name)!.GetValue(actual)),
                $"{name} was not mapped from {expected.GetType().Name} to {actual.GetType().Name}.");
        }
    }
}
