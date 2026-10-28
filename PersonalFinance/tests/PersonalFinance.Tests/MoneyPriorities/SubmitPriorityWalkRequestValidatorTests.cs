using System.Globalization;
using System.Reflection;
using FluentValidation.Results;
using PersonalFinance.ApiService.Features.MoneyPriorities.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// <see cref="SubmitPriorityWalkRequestValidator"/>: every range in the spec's column table, and every follow-up rule
/// (a follow-up is required exactly when its question is on the path; off the path it is ignored, not rejected).
/// </summary>
public class SubmitPriorityWalkRequestValidatorTests
{
    private readonly SubmitPriorityWalkRequestValidator _validator = new();

    /// <summary>A valid request that ends at L1 (APR 22%, balance 5,000).</summary>
    private static SubmitPriorityWalkRequest DebtPath() => new()
    {
        MonthlyAmount = 500m,
        DebtApr = 22m,
        DebtBalance = 5_000m
    };

    /// <summary>A valid request that reaches Q5 (no debt, fund covered, full match, 25 years).</summary>
    private static SubmitPriorityWalkRequest LongHorizonPath() => new()
    {
        MonthlyAmount = 500m,
        DebtApr = 0m,
        EmergencyFundMonths = 6m,
        HasFullEmployerMatch = true,
        HorizonYears = 25,
        AgeBand = RetirementAgeBand.Under50,
        AtContributionLimit = false
    };

    /// <summary>A range message as the validator writes it for a Walk in US dollars (no currency given).</summary>
    private static string InDollars(string template, long dollarMax) => MoneyBounds.Describe(template, dollarMax, null);

    private void AssertValid(SubmitPriorityWalkRequest request)
    {
        ValidationResult result = _validator.Validate(request);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    private void AssertInvalid(SubmitPriorityWalkRequest request, string property, string message)
    {
        ValidationResult result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == property && e.ErrorMessage == message);
    }

    [Fact]
    public void ValidRequests_ForEveryLeaf_Pass()
    {
        AssertValid(DebtPath());
        AssertValid(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 1.5m, MonthlyExpenses = 3_000m
        });
        AssertValid(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 6.99m, EmergencyFundMonths = 3m, HasFullEmployerMatch = false,
            EmployerMatchPercent = 4m
        });
        AssertValid(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = true, HorizonYears = 2
        });
        AssertValid(LongHorizonPath());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000.01")]
    public void MonthlyAmount_OutOfRange_IsRejected(string amount)
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.MonthlyAmount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.MonthlyAmount),
            InDollars(MoneyPrioritiesMessages.MonthlyAmountRange, MoneyPrioritiesLimits.MonthlyAmountMax));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1000000")]
    public void MonthlyAmount_AtTheBounds_Passes(string amount)
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.MonthlyAmount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        AssertValid(request);
    }

    [Fact]
    public void MonthlyAmount_Missing_IsRequired()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.MonthlyAmount = null;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.MonthlyAmount), MoneyPrioritiesMessages.MonthlyAmountRequired);
    }

    [Fact]
    public void MonthlyAmount_WithFractionsOfACent_IsRejected()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.MonthlyAmount = 500.005m;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.MonthlyAmount), MoneyPrioritiesMessages.WholeCents);
    }

    [Fact]
    public void Purpose_IsOptional_UpTo300Characters()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.Purpose = new string('a', MoneyPrioritiesLimits.PurposeMaxLength);
        AssertValid(request);

        request.Purpose = new string('a', MoneyPrioritiesLimits.PurposeMaxLength + 1);
        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.Purpose), MoneyPrioritiesMessages.PurposeTooLong);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("22.999")]
    public void DebtApr_OutOfRange_IsRejected(string apr)
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.DebtApr = decimal.Parse(apr, System.Globalization.CultureInfo.InvariantCulture);

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.DebtApr), MoneyPrioritiesMessages.DebtAprRange);
    }

    [Fact]
    public void DebtApr_Missing_IsRequired()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.DebtApr = null;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.DebtApr), MoneyPrioritiesMessages.DebtAprRequired);
    }

    [Fact]
    public void DebtApr_At100_Passes()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.DebtApr = 100m;

        AssertValid(request);
    }

    [Fact]
    public void DebtBalance_IsRequired_WhenTheAprIs7OrMore()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.DebtApr = 7m;
        request.DebtBalance = null;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.DebtBalance), MoneyPrioritiesMessages.DebtBalanceRequired);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100000000.01")]
    public void DebtBalance_OutOfRange_IsRejected_OnThePath(string balance)
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.DebtBalance = decimal.Parse(balance, System.Globalization.CultureInfo.InvariantCulture);

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.DebtBalance),
            InDollars(MoneyPrioritiesMessages.DebtBalanceRange, MoneyPrioritiesLimits.DebtBalanceMax));
    }

    [Fact]
    public void EmergencyFundMonths_IsRequired_WhenTheAprIsUnder7()
    {
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 6.99m };

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.EmergencyFundMonths),
            MoneyPrioritiesMessages.EmergencyFundMonthsRequired);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("120.1")]
    [InlineData("2.55")]
    public void EmergencyFundMonths_OutOfRange_IsRejected(string months)
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.EmergencyFundMonths = decimal.Parse(months, System.Globalization.CultureInfo.InvariantCulture);
        request.MonthlyExpenses = 3_000m;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.EmergencyFundMonths),
            MoneyPrioritiesMessages.EmergencyFundMonthsRange);
    }

    [Fact]
    public void EmergencyFundMonths_At0And120_Passes()
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.EmergencyFundMonths = 120m;
        AssertValid(request);

        AssertValid(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 0m, MonthlyExpenses = 3_000m
        });
    }

    [Fact]
    public void MonthlyExpenses_IsRequired_WhenTheFundCoversUnder3Months()
    {
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 2.9m };

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.MonthlyExpenses),
            MoneyPrioritiesMessages.MonthlyExpensesRequired);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000000.01")]
    [InlineData("3000.001")]
    public void MonthlyExpenses_OutOfRange_IsRejected_OnThePath(string expenses)
    {
        var request = new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 1m,
            MonthlyExpenses = decimal.Parse(expenses, System.Globalization.CultureInfo.InvariantCulture)
        };

        ValidationResult result = _validator.Validate(request);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitPriorityWalkRequest.MonthlyExpenses));
    }

    [Fact]
    public void HasFullEmployerMatch_IsRequired_WhenTheFundCovers3Months()
    {
        var request = new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m };

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.HasFullEmployerMatch),
            MoneyPrioritiesMessages.HasFullEmployerMatchRequired);
    }

    [Fact]
    public void EmployerMatchPercent_IsRequired_WhenTheMatchIsNotFull()
    {
        var request = new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = false
        };

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.EmployerMatchPercent),
            MoneyPrioritiesMessages.EmployerMatchPercentRequired);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("4.125")]
    public void EmployerMatchPercent_OutOfRange_IsRejected_OnThePath(string percent)
    {
        var request = new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = false,
            EmployerMatchPercent = decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)
        };

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.EmployerMatchPercent),
            MoneyPrioritiesMessages.EmployerMatchPercentRange);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    public void EmployerMatchPercent_At0And100_Passes(string percent)
    {
        AssertValid(new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = false,
            EmployerMatchPercent = decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    [Fact]
    public void HorizonYears_IsRequired_WhenTheMatchIsFull()
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.HorizonYears = null;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.HorizonYears), MoneyPrioritiesMessages.HorizonYearsRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void HorizonYears_OutOfRange_IsRejected(int years)
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.HorizonYears = years;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.HorizonYears), MoneyPrioritiesMessages.HorizonYearsRange);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void HorizonYears_At1And50_Passes(int years)
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.HorizonYears = years;

        AssertValid(request);
    }

    [Fact]
    public void AgeBandAndLimit_AreRequired_WhenTheHorizonIsOver10Years()
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.HorizonYears = 11;
        request.AgeBand = null;
        request.AtContributionLimit = null;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.AgeBand), MoneyPrioritiesMessages.AgeBandRequired);
        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.AtContributionLimit),
            MoneyPrioritiesMessages.AtContributionLimitRequired);
    }

    [Fact]
    public void AgeBand_Unknown_IsRejected()
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.AgeBand = (RetirementAgeBand)99;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.AgeBand), MoneyPrioritiesMessages.AgeBandRequired);
    }

    [Fact]
    public void AgeBandAndLimit_AreNotAsked_At10YearsOrUnder()
    {
        SubmitPriorityWalkRequest request = LongHorizonPath();
        request.HorizonYears = 10;
        request.AgeBand = null;
        request.AtContributionLimit = null;

        AssertValid(request);
    }

    [Fact]
    public void OffPathFollowUps_AreIgnored_EvenWhenOutOfRange()
    {
        // L1 path: nothing after Q1 is asked, so invalid later answers do not reject the request.
        SubmitPriorityWalkRequest request = DebtPath();
        request.EmergencyFundMonths = -5m;
        request.MonthlyExpenses = -1m;
        request.EmployerMatchPercent = 500m;
        request.HorizonYears = 999;
        request.AgeBand = (RetirementAgeBand)99;

        AssertValid(request);

        // Q5 path: the debt balance is off the path.
        SubmitPriorityWalkRequest longHorizon = LongHorizonPath();
        longHorizon.DebtBalance = -100m;
        longHorizon.MonthlyExpenses = 0m;
        longHorizon.EmployerMatchPercent = -3m;

        AssertValid(longHorizon);
    }

    /// <summary>Requests that, between them, break most rules: missing answers, out-of-range values and extra cents.</summary>
    private static IEnumerable<SubmitPriorityWalkRequest> InvalidRequests() =>
    [
        new SubmitPriorityWalkRequest(),
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 2_000_000m, DebtApr = 200m, DebtBalance = -1m, Purpose = new string('x', 301)
        },
        new SubmitPriorityWalkRequest { MonthlyAmount = 10.001m, DebtApr = 22m, DebtBalance = 2_000_000m },
        new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 1m },
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 1m, MonthlyExpenses = 2_000_000m
        },
        new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 500m },
        new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m },
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = false,
            EmployerMatchPercent = 500m
        },
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = true
        },
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = true,
            HorizonYears = 99
        },
        new SubmitPriorityWalkRequest
        {
            MonthlyAmount = 500m, DebtApr = 0m, EmergencyFundMonths = 3m, HasFullEmployerMatch = true,
            HorizonYears = 25
        }
    ];

    private List<ValidationFailure> FailuresOf(IEnumerable<SubmitPriorityWalkRequest> requests) =>
        requests.SelectMany(request => _validator.Validate(request).Errors).ToList();

    [Fact]
    public void EveryFailure_HasAKeyFromApiErrorKeys()
    {
        HashSet<string> keys = typeof(ApiErrorKeys.MoneyPriorities)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        List<ValidationFailure> failures = FailuresOf(InvalidRequests());

        Assert.NotEmpty(failures);
        Assert.All(failures, failure => Assert.Contains(failure.ErrorCode, keys));
    }

    [Fact]
    public void DongAmountsOverTheDollarBounds_Pass_OnEveryMoneyField()
    {
        SubmitPriorityWalkRequest debt = DebtPath();
        debt.CurrencyCode = "VND";
        debt.MonthlyAmount = 30_000_000m;
        debt.DebtBalance = 2_000_000_000m;
        AssertValid(debt);

        AssertValid(new SubmitPriorityWalkRequest
        {
            CurrencyCode = "VND", MonthlyAmount = 30_000_000m, DebtApr = 0m, EmergencyFundMonths = 1m,
            MonthlyExpenses = 30_000_000m
        });
    }

    [Fact]
    public void TheSameFigureInDollars_IsStillRejected()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.CurrencyCode = "USD";
        request.MonthlyAmount = 30_000_000m;

        AssertInvalid(request, nameof(SubmitPriorityWalkRequest.MonthlyAmount),
            InDollars(MoneyPrioritiesMessages.MonthlyAmountRange, MoneyPrioritiesLimits.MonthlyAmountMax));
    }

    [Fact]
    public void ADongAmountOverTheDongBound_IsRejected_WithTheDongBoundAsArgument()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.CurrencyCode = "VND";
        request.MonthlyAmount = 25_000_000_000.01m;

        ValidationFailure failure = Assert.Single(_validator.Validate(request).Errors);

        Assert.Equal(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange, failure.ErrorCode);
        Assert.Equal("Enter a monthly amount over ₫0 and at most ₫25,000,000,000.", failure.ErrorMessage);
        IReadOnlyDictionary<string, ApiErrorArgDto> args =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
        Assert.Equal("25000000000", args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Money, args["max"].Kind);
        Assert.Equal("VND", args["max"].Currency);
        Assert.Equal("VND", args["min"].Currency);
    }

    [Fact]
    public void RangeFailures_CarryMinAndMaxMoneyArgs()
    {
        SubmitPriorityWalkRequest request = DebtPath();
        request.MonthlyAmount = 2_000_000m;

        ValidationFailure failure = Assert.Single(_validator.Validate(request).Errors);

        Assert.Equal(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange, failure.ErrorCode);
        IReadOnlyDictionary<string, ApiErrorArgDto> args =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, ApiErrorArgDto>>(failure.CustomState);
        Assert.Equal("1000000", args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Money, args["max"].Kind);
        Assert.Equal("USD", args["max"].Currency);
        Assert.Equal("0", args["min"].Value);
        Assert.Equal(ApiErrorArgKind.Money, args["min"].Kind);
        Assert.Equal("USD", args["min"].Currency);
    }

    [Fact]
    public void ResolvingEveryFailureInEnglish_LeavesNoPlaceholder()
    {
        using var uiCulture = new UiCultureScope("en");
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            ApiErrorDto body = new ValidationResult(FailuresOf(InvalidRequests())).ToApiError();

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
}
