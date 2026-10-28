using System.Globalization;
using System.Reflection;
using FluentValidation.Results;
using PersonalFinance.ApiService.Features.BudgetPlanner.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.BudgetPlanner;

/// <summary>
/// Unit tests for <see cref="SaveBudgetRequestValidator"/>: the income and allocation ranges, and that every failure
/// carries an <see cref="ApiErrorKeys.BudgetPlanner"/> key, names its field and resolves in English without a
/// placeholder left (localized API errors spec §2.4).
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
    [InlineData(10_000_000.01)]
    public void Validate_WhenIncomeIsOutOfRange_FailsWithTheIncomeRangeKey(decimal income)
    {
        var request = new SaveBudgetRequestDto { MonthlyIncome = income, Allocations = new BudgetAllocationsDto() };

        ValidationFailure failure = Assert.Single(_validator.Validate(request).Errors);

        Assert.Equal(ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange, failure.ErrorCode);
        Assert.Equal(nameof(SaveBudgetRequestDto.MonthlyIncome), failure.PropertyName);
        IReadOnlyDictionary<string, ApiErrorArgDto> args =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
        Assert.Equal("0", args["min"].Value);
        Assert.Equal("10000000", args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Money, args["max"].Kind);
        Assert.Equal("USD", args["max"].Currency);
    }

    [Fact]
    public void Validate_WhenAnAllocationIsOutOfRange_NamesTheFieldPath()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 1000m,
            Allocations = new BudgetAllocationsDto { DebtRepayment = -1m, Entertainment = 10_000_000.01m }
        };

        List<ValidationFailure> failures = _validator.Validate(request).Errors;

        Assert.Equal(2, failures.Count);
        Assert.All(failures, failure => Assert.Equal(ApiErrorKeys.BudgetPlanner.AllocationRange, failure.ErrorCode));
        Assert.Contains(failures, failure => failure.PropertyName == "Allocations.DebtRepayment");
        Assert.Contains(failures, failure => failure.PropertyName == "Allocations.Entertainment");
    }

    [Fact]
    public void Validate_ADongIncomeOverTheDollarBound_IsValid_AndTheSameFigureInDollarsIsNot()
    {
        var request = new SaveBudgetRequestDto
        {
            CurrencyCode = "VND", MonthlyIncome = 30_000_000m, Allocations = new BudgetAllocationsDto { Housing = 20_000_000m }
        };

        Assert.True(_validator.Validate(request).IsValid);

        request.CurrencyCode = "USD";
        List<ValidationFailure> failures = _validator.Validate(request).Errors;

        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, f => f.ErrorCode == ApiErrorKeys.BudgetPlanner.MonthlyIncomeRange);
        Assert.Contains(failures, f => f.ErrorCode == ApiErrorKeys.BudgetPlanner.AllocationRange);
        Assert.All(failures, f => Assert.Contains("$10,000,000", f.ErrorMessage));
    }

    [Fact]
    public void Validate_ADongAmountOverTheDongBound_FailsWithTheDongBoundAsArgument()
    {
        var request = new SaveBudgetRequestDto
        {
            CurrencyCode = "VND",
            MonthlyIncome = 250_000_000_000.01m,
            Allocations = new BudgetAllocationsDto { Housing = 250_000_000_001m }
        };

        List<ValidationFailure> failures = _validator.Validate(request).Errors;

        Assert.Equal(2, failures.Count);
        Assert.All(failures, failure =>
        {
            IReadOnlyDictionary<string, ApiErrorArgDto> args =
                Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
            Assert.Equal("250000000000", args["max"].Value);
            Assert.Equal(ApiErrorArgKind.Money, args["max"].Kind);
            Assert.Equal("VND", args["max"].Currency);
            Assert.Equal("VND", args["min"].Currency);
            Assert.Contains("₫250,000,000,000", failure.ErrorMessage);
        });
    }

    [Fact]
    public void Validate_WhenAnAllocationHasFractionsOfACent_FailsWithTheWholeCentsKey()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 1000m,
            Allocations = new BudgetAllocationsDto { Food = 12.345m }
        };

        ValidationFailure failure = Assert.Single(_validator.Validate(request).Errors);

        Assert.Equal(ApiErrorKeys.BudgetPlanner.AllocationWholeCents, failure.ErrorCode);
        Assert.Equal("Allocations.Food", failure.PropertyName);
    }

    [Fact]
    public void EveryAllocationField_IsValidated()
    {
        var request = new SaveBudgetRequestDto
        {
            MonthlyIncome = 1000m,
            Allocations = new BudgetAllocationsDto
            {
                Housing = -1m, Food = -1m, Transportation = -1m, Utilities = -1m, Insurance = -1m,
                Healthcare = -1m, Savings = -1m, DebtRepayment = -1m, Entertainment = -1m, Miscellaneous = -1m
            }
        };

        List<string> fields = _validator.Validate(request).Errors.Select(failure => failure.PropertyName).ToList();

        IEnumerable<string> expected = typeof(BudgetAllocationsDto).GetProperties()
            .Select(property => $"{nameof(SaveBudgetRequestDto.Allocations)}.{property.Name}");
        Assert.Equal(expected.Order(StringComparer.Ordinal), fields.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryFailure_HasAKeyFromApiErrorKeys()
    {
        HashSet<string> keys = typeof(ApiErrorKeys.BudgetPlanner)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        List<ValidationFailure> failures = AllFailures();

        Assert.NotEmpty(failures);
        Assert.All(failures, failure => Assert.Contains(failure.ErrorCode, keys));
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

    /// <summary>Requests that, between them, break every rule: income at both bounds, an allocation at both bounds and one with extra cents.</summary>
    private List<ValidationFailure> AllFailures() =>
        new[]
        {
            new SaveBudgetRequestDto { MonthlyIncome = 0m, Allocations = new BudgetAllocationsDto { Housing = -1m } },
            new SaveBudgetRequestDto
            {
                MonthlyIncome = 10_000_000.01m,
                Allocations = new BudgetAllocationsDto { Savings = 10_000_000.01m, Food = 0.001m }
            }
        }.SelectMany(request => _validator.Validate(request).Errors).ToList();
}
