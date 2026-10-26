using FluentValidation.Results;
using PersonalFinance.ApiService.Features.SavingsGoals.Validators;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Tests.SavingsGoals;

/// <summary>
/// A Contribution's amount is never zero, its date is between 2000 and today (UTC, as Accounts have no time zone yet),
/// and its note is short; a corrected balance and a goal's starting amount are never negative.
/// </summary>
public class ContributionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 23, 59, 0, TimeSpan.Zero);

    private readonly SaveContributionRequestValidator _contribution = new(new FixedTimeProvider(Now));

    [Theory]
    [InlineData(25.5)]
    [InlineData(-25.5)]
    public void ADepositOrWithdrawal_DatedToday_IsValid(double amount)
    {
        var request = new SaveContributionRequestDto { Amount = (decimal)amount, Date = new DateOnly(2026, 10, 25) };

        Assert.True(_contribution.Validate(request).IsValid);
    }

    [Fact]
    public void AContribution_WithoutADate_IsValid()
    {
        Assert.True(_contribution.Validate(new SaveContributionRequestDto { Amount = 1m }).IsValid);
    }

    [Fact]
    public void AZeroAmount_IsRefused()
    {
        AssertRefused(new SaveContributionRequestDto { Amount = 0m }, "Contribution amount must not be zero.");
    }

    [Fact]
    public void AnAmountWithMoreThanTwoDecimals_IsRefused()
    {
        AssertRefused(new SaveContributionRequestDto { Amount = 1.005m },
            "Contribution amount cannot have more than 2 decimal places.");
    }

    [Fact]
    public void ADateAfterToday_IsRefused()
    {
        AssertRefused(new SaveContributionRequestDto { Amount = 1m, Date = new DateOnly(2026, 10, 26) },
            "Contribution date cannot be in the future.");
    }

    [Fact]
    public void ADateBefore2000_IsRefused()
    {
        AssertRefused(new SaveContributionRequestDto { Amount = 1m, Date = new DateOnly(1999, 12, 31) },
            "Contribution date cannot be before the year 2000.");
    }

    [Fact]
    public void ANoteOver200Characters_IsRefused()
    {
        Assert.True(_contribution.Validate(new SaveContributionRequestDto { Amount = 1m, Note = new string('n', 200) })
            .IsValid);
        AssertRefused(new SaveContributionRequestDto { Amount = 1m, Note = new string('n', 201) },
            "Note must not exceed 200 characters.");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1200.5, true)]
    [InlineData(-0.01, false)]
    public void ACorrectedBalance_IsZeroOrMore(double balance, bool valid)
    {
        ValidationResult result = new CorrectBalanceRequestValidator()
            .Validate(new CorrectBalanceRequestDto { Balance = (decimal)balance });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void AGoalsStartingAmount_IsNeverNegative(int? starting, bool valid)
    {
        ValidationResult result = new CreateSavingsGoalRequestValidator().Validate(new CreateSavingsGoalRequestDto
        {
            Name = "Emergency Fund",
            TargetAmount = 1_000m,
            CurrentAmount = starting
        });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void AGoal_WithoutANameOrPositiveTarget_IsRefused()
    {
        ValidationResult result = new CreateSavingsGoalRequestValidator()
            .Validate(new CreateSavingsGoalRequestDto { Name = "", TargetAmount = 0m });

        Assert.Equal(2, result.Errors.Count);
    }

    private void AssertRefused(SaveContributionRequestDto request, string message)
    {
        ValidationResult result = _contribution.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == message);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
