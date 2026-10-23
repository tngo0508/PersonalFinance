using FluentValidation.Results;
using PersonalFinance.ApiService.Features.BudgetPlanner.Validators;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.Tests.BudgetPlanner;

/// <summary>
/// Unit tests for <see cref="SaveBudgetRequestValidator"/>.
/// </summary>
public class SaveBudgetRequestValidatorTests
{
    private readonly SaveBudgetRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenAllocationsSumToLessThanIncome_IsValid()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 3575.00m,
            Allocations = new BudgetAllocationsDto
            {
                Housing = 700.00m,
                Food = 643.14m,
                Transportation = 0.00m,
                Utilities = 199.84m,
                Insurance = 175.18m,
                Healthcare = 375.02m,
                Savings = 500.00m,
                DebtRepayment = 300.00m,
                Entertainment = 200.00m,
                Miscellaneous = 417.49m // total = 3510.67m < 3575.00m
            }
        };

        ValidationResult result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenAllocationsSumToExactlyIncome_IsValid()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 1000.00m,
            Allocations = new BudgetAllocationsDto
            {
                Housing = 500.00m,
                Food = 300.00m,
                Savings = 200.00m
            }
        };

        ValidationResult result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenAllocationsAreZero_IsValid()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 2500.00m,
            Allocations = new BudgetAllocationsDto()
        };

        ValidationResult result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenAllocationsExceedIncome_IsValid()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 1000.00m,
            Allocations = new BudgetAllocationsDto
            {
                Housing = 600.00m,
                Food = 500.00m // total = 1100.00m > 1000.00m
            }
        };

        ValidationResult result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_WhenIncomeIsZeroOrNegative_IsInvalid(decimal income)
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = income,
            Allocations = new BudgetAllocationsDto()
        };

        ValidationResult result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }
}
