using PersonalFinance.ApiService.Validators;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests;

/// <summary>
/// Unit tests for Budget Tracker FluentValidation validators.
/// </summary>
public class BudgetTrackerValidatorTests
{
    [Fact]
    public void InitializeMonthRequestValidator_ValidatesRanges()
    {
        var validator = new InitializeMonthRequestValidator();

        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 2026, Month = 10 }).IsValid);
        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 1900, Month = 1 }).IsValid);
        Assert.True(validator.Validate(new InitializeMonthRequest { Year = 2100, Month = 12 }).IsValid);

        Assert.False(validator.Validate(new InitializeMonthRequest { Year = 1899, Month = 10 }).IsValid);
        Assert.False(validator.Validate(new InitializeMonthRequest { Year = 2101, Month = 10 }).IsValid);
        Assert.False(validator.Validate(new InitializeMonthRequest { Year = 2026, Month = 0 }).IsValid);
        Assert.False(validator.Validate(new InitializeMonthRequest { Year = 2026, Month = 13 }).IsValid);
    }

    [Fact]
    public void SetPlannedRequestValidator_ValidatesAmount()
    {
        var validator = new SetPlannedRequestValidator();

        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 0m }).IsValid);
        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 150.50m }).IsValid);
        Assert.True(validator.Validate(new SetPlannedRequest { Amount = 9_999_999_999_999_999.99m }).IsValid);

        // Negative
        Assert.False(validator.Validate(new SetPlannedRequest { Amount = -0.01m }).IsValid);
        // More than 2 decimal places
        Assert.False(validator.Validate(new SetPlannedRequest { Amount = 100.123m }).IsValid);
        // Exceeds upper bound
        Assert.False(validator.Validate(new SetPlannedRequest { Amount = 10_000_000_000_000_000.00m }).IsValid);
    }

    [Fact]
    public void SetActualOverrideRequestValidator_ValidatesAmount()
    {
        var validator = new SetActualOverrideRequestValidator();

        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = null }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 0m }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 250.75m }).IsValid);
        Assert.True(validator.Validate(new SetActualOverrideRequest { Amount = 9_999_999_999_999_999.99m }).IsValid);

        // Negative
        Assert.False(validator.Validate(new SetActualOverrideRequest { Amount = -5m }).IsValid);
        // More than 2 decimal places
        Assert.False(validator.Validate(new SetActualOverrideRequest { Amount = 50.999m }).IsValid);
        // Exceeds upper bound
        Assert.False(validator.Validate(new SetActualOverrideRequest { Amount = 10_000_000_000_000_000.00m }).IsValid);
    }

    [Fact]
    public void SaveCategoryRequestValidator_ValidatesFields()
    {
        var validator = new SaveCategoryRequestValidator();

        Assert.True(validator.Validate(new SaveCategoryRequest
            { Name = "Groceries", Kind = BudgetCategoryKind.Expense, SortOrder = 1 }).IsValid);

        // Empty / whitespace name
        Assert.False(validator.Validate(new SaveCategoryRequest
            { Name = "", Kind = BudgetCategoryKind.Expense, SortOrder = 1 }).IsValid);
        Assert.False(validator.Validate(new SaveCategoryRequest
            { Name = "   ", Kind = BudgetCategoryKind.Expense, SortOrder = 1 }).IsValid);

        // Name exceeding 100 characters
        Assert.False(validator.Validate(new SaveCategoryRequest
            { Name = new string('A', 101), Kind = BudgetCategoryKind.Expense, SortOrder = 1 }).IsValid);

        // Invalid enum
        Assert.False(validator.Validate(new SaveCategoryRequest
            { Name = "Valid", Kind = (BudgetCategoryKind)99, SortOrder = 1 }).IsValid);

        // Negative sort order
        Assert.False(validator.Validate(new SaveCategoryRequest
            { Name = "Valid", Kind = BudgetCategoryKind.Income, SortOrder = -1 }).IsValid);
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
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = default,
            Amount = 10m,
            CategoryId = 1
        }).IsValid);

        // Zero / negative amount
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 0m,
            CategoryId = 1
        }).IsValid);

        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = -10m,
            CategoryId = 1
        }).IsValid);

        // More than 2 decimal places
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 10.555m,
            CategoryId = 1
        }).IsValid);

        // Exceeds upper bound
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 10_000_000_000_000_000.00m,
            CategoryId = 1
        }).IsValid);

        // Description exceeding 200 characters
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 25m,
            Description = new string('D', 201),
            CategoryId = 1
        }).IsValid);

        // Invalid category ID
        Assert.False(validator.Validate(new SaveTransactionRequest
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 25m,
            CategoryId = 0
        }).IsValid);
    }

    [Fact]
    public void SetBudgetDataSourceRequestValidator_ValidatesEnum()
    {
        var validator = new SetBudgetDataSourceRequestValidator();

        Assert.True(
            validator.Validate(new SetBudgetDataSourceRequest { Source = BudgetDataSource.GoogleSheet }).IsValid);
        Assert.True(validator.Validate(new SetBudgetDataSourceRequest { Source = BudgetDataSource.BudgetTracker })
            .IsValid);
        Assert.False(validator.Validate(new SetBudgetDataSourceRequest { Source = (BudgetDataSource)99 }).IsValid);
    }
}