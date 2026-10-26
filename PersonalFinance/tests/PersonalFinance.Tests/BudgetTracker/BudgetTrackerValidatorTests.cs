using System.Globalization;
using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using PersonalFinance.ApiService.Features.BudgetTracker.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// Unit tests for the Budget Tracker FluentValidation validators: the ranges each enforces, and that every failure
/// carries an <see cref="ApiErrorKeys.BudgetTracker"/> key whose English text resolves without a placeholder left
/// (localized API errors spec §2.4).
/// </summary>
public class BudgetTrackerValidatorTests
{
    private static readonly HashSet<string> BudgetTrackerKeys = typeof(ApiErrorKeys.BudgetTracker)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void InitializeMonthRequestValidator_ValidatesRanges()
    {
        var validator = new InitializeMonthRequestValidator();

        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 2026, Month = 10 }).IsValid);
        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 1900, Month = 1 }).IsValid);
        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 2100, Month = 12 }).IsValid);

        AssertCode(validator, new InitializeMonthRequest { Year = 1899, Month = 10 }, ApiErrorKeys.BudgetTracker.YearRange);
        AssertCode(validator, new InitializeMonthRequest { Year = 2101, Month = 10 }, ApiErrorKeys.BudgetTracker.YearRange);
        AssertCode(validator, new InitializeMonthRequest { Year = 2026, Month = 0 }, ApiErrorKeys.BudgetTracker.MonthRange);
        AssertCode(validator, new InitializeMonthRequest { Year = 2026, Month = 13 }, ApiErrorKeys.BudgetTracker.MonthRange);
    }

    [Fact]
    public void InitializeMonthRequestValidator_YearFailure_CarriesTheBoundsAsNumbers()
    {
        ValidationFailure failure = Assert.Single(
            new InitializeMonthRequestValidator().Validate(new InitializeMonthRequest { Year = 1899, Month = 10 }).Errors);

        IReadOnlyDictionary<string, ApiErrorArgDto> args =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
        Assert.Equal("1900", args["min"].Value);
        Assert.Equal("2100", args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Number, args["max"].Kind);
    }

    [Fact]
    public void SetPlannedRequestValidator_ValidatesAmount()
    {
        var validator = new SetPlannedRequestValidator();

        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 0m }).IsValid);
        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 150.50m }).IsValid);
        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 9_999_999_999_999_999.99m }).IsValid);

        AssertCode(validator, new SetPlannedRequest { Amount = -0.01m }, ApiErrorKeys.BudgetTracker.PlannedAmountRange);
        AssertCode(validator, new SetPlannedRequest { Amount = 100.123m }, ApiErrorKeys.BudgetTracker.PlannedAmountWholeCents);
        AssertCode(validator, new SetPlannedRequest { Amount = 10_000_000_000_000_000.00m },
            ApiErrorKeys.BudgetTracker.PlannedAmountRange);
    }

    [Fact]
    public void SetPlannedRequestValidator_RangeFailure_CarriesMinAndMaxMoneyArgs()
    {
        ValidationFailure failure = Assert.Single(
            new SetPlannedRequestValidator().Validate(new SetPlannedRequest { Amount = -1m }).Errors);

        IReadOnlyDictionary<string, ApiErrorArgDto> args =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
        Assert.Equal("0", args["min"].Value);
        Assert.Equal("9999999999999999.99", args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Money, args["max"].Kind);
        Assert.Equal("USD", args["max"].Currency);
    }

    [Fact]
    public void SetActualOverrideRequestValidator_ValidatesAmount()
    {
        var validator = new SetActualOverrideRequestValidator();

        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = null }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 0m }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 250.75m }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 9_999_999_999_999_999.99m }).IsValid);

        AssertCode(validator, new SetActualOverrideRequest { Amount = -5m }, ApiErrorKeys.BudgetTracker.ActualOverrideRange);
        AssertCode(validator, new SetActualOverrideRequest { Amount = 50.999m },
            ApiErrorKeys.BudgetTracker.ActualOverrideWholeCents);
        AssertCode(validator, new SetActualOverrideRequest { Amount = 10_000_000_000_000_000.00m },
            ApiErrorKeys.BudgetTracker.ActualOverrideRange);
    }

    [Fact]
    public void SaveCategoryRequestValidator_ValidatesFields()
    {
        var validator = new SaveCategoryRequestValidator();

        Assert.True(validator.Validate(new SaveCategoryRequest
        { Name = "Groceries", Kind = BudgetCategoryKind.Expense, SortOrder = 1 }).IsValid);

        // Empty / whitespace name
        AssertCode(validator, new SaveCategoryRequest { Name = "", Kind = BudgetCategoryKind.Expense, SortOrder = 1 },
            ApiErrorKeys.BudgetTracker.CategoryNameRequired);
        AssertCode(validator, new SaveCategoryRequest { Name = "   ", Kind = BudgetCategoryKind.Expense, SortOrder = 1 },
            ApiErrorKeys.BudgetTracker.CategoryNameRequired);

        // Name exceeding 100 characters
        AssertCode(validator,
            new SaveCategoryRequest { Name = new string('A', 101), Kind = BudgetCategoryKind.Expense, SortOrder = 1 },
            ApiErrorKeys.BudgetTracker.CategoryNameTooLong);

        // Invalid enum
        AssertCode(validator, new SaveCategoryRequest { Name = "Valid", Kind = (BudgetCategoryKind)99, SortOrder = 1 },
            ApiErrorKeys.BudgetTracker.CategoryKindInvalid);

        // Negative sort order
        AssertCode(validator, new SaveCategoryRequest { Name = "Valid", Kind = BudgetCategoryKind.Income, SortOrder = -1 },
            ApiErrorKeys.BudgetTracker.SortOrderNegative);
    }

    [Fact]
    public void SaveTransactionRequestValidator_ValidatesFields()
    {
        var validator = new SaveTransactionRequestValidator();

        Assert.True(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 45.99m,
            Description = "Coffee",
            CategoryId = 1
        }).IsValid);

        // Missing date (default)
        AssertCode(validator, new SaveTransactionRequest { Date = default, Amount = 10m, CategoryId = 1 },
            ApiErrorKeys.BudgetTracker.TransactionDateRequired);

        // Zero / negative amount
        AssertCode(validator, Transaction(0m), ApiErrorKeys.BudgetTracker.TransactionAmountRange);
        AssertCode(validator, Transaction(-10m), ApiErrorKeys.BudgetTracker.TransactionAmountRange);

        // More than 2 decimal places
        AssertCode(validator, Transaction(10.555m), ApiErrorKeys.BudgetTracker.TransactionAmountWholeCents);

        // Exceeds upper bound
        AssertCode(validator, Transaction(10_000_000_000_000_000.00m), ApiErrorKeys.BudgetTracker.TransactionAmountRange);

        // Description exceeding 200 characters
        SaveTransactionRequest longDescription = Transaction(25m);
        longDescription.Description = new string('D', 201);
        AssertCode(validator, longDescription, ApiErrorKeys.BudgetTracker.DescriptionTooLong);

        // Invalid category ID
        SaveTransactionRequest noCategory = Transaction(25m);
        noCategory.CategoryId = 0;
        AssertCode(validator, noCategory, ApiErrorKeys.BudgetTracker.CategoryRequired);
    }

    [Fact]
    public void SetBudgetDataSourceRequestValidator_ValidatesEnum()
    {
        var validator = new SetBudgetDataSourceRequestValidator();

        Assert.True(
            validator.Validate(new SetBudgetDataSourceRequest { Source = BudgetDataSource.GoogleSheet }).IsValid);
        Assert.True(validator.Validate(new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker })
            .IsValid);
        AssertCode(validator, new SetBudgetDataSourceRequest { Source = (BudgetDataSource)99 },
            ApiErrorKeys.BudgetTracker.DataSourceInvalid);
    }

    [Fact]
    public void EveryFailure_HasAKeyFromApiErrorKeys()
    {
        List<ValidationFailure> failures = AllFailures();

        Assert.NotEmpty(failures);
        Assert.All(failures, failure => Assert.Contains(failure.ErrorCode, BudgetTrackerKeys));
    }

    [Fact]
    public void ResolvingEveryFailureInEnglish_LeavesNoPlaceholder()
    {
        using var uiCulture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            ApiErrorDto body = new ValidationResult(AllFailures()).ToApiError();

            Assert.NotEmpty(body.Errors);
            foreach (ApiErrorEntryDto entry in body.Errors)
            {
                Assert.False(TestLocalization.Localizer[entry.Key].ResourceNotFound, entry.Key);
                string text = ApiErrorMessages.Resolve(TestLocalization.Localizer, entry);
                Assert.DoesNotContain("{", text);
                Assert.DoesNotContain("}", text);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
        }
    }

    /// <summary>One failing request per rule of every Budget Tracker validator.</summary>
    private static List<ValidationFailure> AllFailures()
    {
        var failures = new List<ValidationFailure>();
        var month = new InitializeMonthRequestValidator();
        failures.AddRange(month.Validate(new InitializeMonthRequest { Year = 1899, Month = 13 }).Errors);

        var planned = new SetPlannedRequestValidator();
        failures.AddRange(planned.Validate(new SetPlannedRequest { Amount = -1m }).Errors);
        failures.AddRange(planned.Validate(new SetPlannedRequest { Amount = 10_000_000_000_000_000.00m }).Errors);
        failures.AddRange(planned.Validate(new SetPlannedRequest { Amount = 1.234m }).Errors);

        var actual = new SetActualOverrideRequestValidator();
        failures.AddRange(actual.Validate(new SetActualOverrideRequest { Amount = -1m }).Errors);
        failures.AddRange(actual.Validate(new SetActualOverrideRequest { Amount = 10_000_000_000_000_000.00m }).Errors);
        failures.AddRange(actual.Validate(new SetActualOverrideRequest { Amount = 1.234m }).Errors);

        var category = new SaveCategoryRequestValidator();
        failures.AddRange(category.Validate(new SaveCategoryRequest { Name = "", Kind = (BudgetCategoryKind)99, SortOrder = -1 }).Errors);
        failures.AddRange(category.Validate(new SaveCategoryRequest { Name = new string('A', 101), Kind = BudgetCategoryKind.Expense }).Errors);

        var transaction = new SaveTransactionRequestValidator();
        failures.AddRange(transaction.Validate(new SaveTransactionRequest
        {
            Date = default, Amount = 0m, Description = new string('D', 201), CategoryId = 0
        }).Errors);
        failures.AddRange(transaction.Validate(Transaction(10_000_000_000_000_000.00m)).Errors);
        failures.AddRange(transaction.Validate(Transaction(1.234m)).Errors);

        var source = new SetBudgetDataSourceRequestValidator();
        failures.AddRange(source.Validate(new SetBudgetDataSourceRequest { Source = (BudgetDataSource)99 }).Errors);
        return failures;
    }

    private static SaveTransactionRequest Transaction(decimal amount) =>
        new() { Date = new DateOnly(2026, 10, 15), Amount = amount, CategoryId = 1 };

    private static void AssertCode<T>(IValidator<T> validator, T request, string expectedCode)
    {
        ValidationResult result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == expectedCode);
    }
}
