using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The currency schema of slice C1 (ADR 0016, localization spec §2.5): one <c>char(3)</c> code per money-bearing row,
/// required and <c>USD</c> by default, never per field; and the <see cref="ExchangeRate"/> table, unique per pair and day.
/// Read from the model the SQL Server migrations are generated from.
/// </summary>
public class CurrencySchemaTests
{
    private static readonly IModel Model = AzureSqlTranslation.CreateContext().Model;

    public static TheoryData<Type, string> CurrencyColumns => new()
    {
        { typeof(NetWorthParameters), nameof(NetWorthParameters.CurrencyCode) },
        { typeof(UserBudgetAllocation), nameof(UserBudgetAllocation.CurrencyCode) },
        { typeof(PriorityWalk), nameof(PriorityWalk.CurrencyCode) },
        { typeof(BudgetTransaction), nameof(BudgetTransaction.CurrencyCode) },
        { typeof(MonthlyBudgetLine), nameof(MonthlyBudgetLine.CurrencyCode) },
        { typeof(SavingsGoal), nameof(SavingsGoal.CurrencyCode) },
        { typeof(SpreadsheetBudgetSnapshot), nameof(SpreadsheetBudgetSnapshot.CurrencyCode) },
        { typeof(SpreadsheetTransactionRecord), nameof(SpreadsheetTransactionRecord.CurrencyCode) },
        { typeof(GoogleDriveConnection), nameof(GoogleDriveConnection.CurrencyCode) },
        { typeof(UserPreference), nameof(UserPreference.ReportingCurrency) }
    };

    [Theory]
    [MemberData(nameof(CurrencyColumns))]
    public void EachCurrencyColumn_IsARequiredChar3_DefaultingToUsd(Type entity, string column)
    {
        IProperty property = Model.FindEntityType(entity)!.FindProperty(column)!;

        Assert.False(property.IsNullable);
        Assert.Equal("char(3)", property.GetColumnType());
        Assert.Equal(SupportedCurrencies.DefaultCode, property.GetDefaultValue());
        object newRow = Activator.CreateInstance(entity)!;
        Assert.Equal(SupportedCurrencies.DefaultCode, entity.GetProperty(column)!.GetValue(newRow));
    }

    [Fact]
    public void NoOtherEntity_CarriesACurrencyCode_SoAmountsNeverGetOnePerField()
    {
        HashSet<Type> expected = CurrencyColumns.Select(row => (Type)row[0]).ToHashSet();

        List<string> unexpected = Model.GetEntityTypes()
            .Where(type => type.GetProperties().Any(p => p.Name.Contains("Currency", StringComparison.Ordinal)))
            .Where(type => !expected.Contains(type.ClrType) && type.ClrType != typeof(ExchangeRate))
            .Select(type => type.ClrType.Name)
            .ToList();
        List<string> twoCodes = Model.GetEntityTypes()
            .Where(type => expected.Contains(type.ClrType))
            .Where(type => type.GetProperties().Count(p => p.GetColumnType() == "char(3)") != 1)
            .Select(type => type.ClrType.Name)
            .ToList();

        Assert.Empty(unexpected);
        Assert.Empty(twoCodes);
    }

    [Fact]
    public void ExchangeRate_IsUniquePerBaseQuoteAndDay_WithAnEightDecimalRate()
    {
        IEntityType rates = Model.FindEntityType(typeof(ExchangeRate))!;

        IIndex index = Assert.Single(rates.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal([nameof(ExchangeRate.BaseCurrency), nameof(ExchangeRate.QuoteCurrency), nameof(ExchangeRate.Date)],
            index.Properties.Select(p => p.Name));
        Assert.Equal("decimal(18,8)", rates.FindProperty(nameof(ExchangeRate.Rate))!.GetColumnType());
        Assert.Equal("char(3)", rates.FindProperty(nameof(ExchangeRate.BaseCurrency))!.GetColumnType());
        Assert.Equal("char(3)", rates.FindProperty(nameof(ExchangeRate.QuoteCurrency))!.GetColumnType());
        Assert.Equal("date", rates.FindProperty(nameof(ExchangeRate.Date))!.GetColumnType());
    }
}
