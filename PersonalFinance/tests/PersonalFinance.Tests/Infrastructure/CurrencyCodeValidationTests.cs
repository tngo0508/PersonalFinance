using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.BudgetPlanner.Validators;
using PersonalFinance.ApiService.Features.BudgetTracker.Validators;
using PersonalFinance.ApiService.Features.GoogleDrive;
using PersonalFinance.ApiService.Features.MoneyPriorities.Validators;
using PersonalFinance.ApiService.Features.NetWorth.Validators;
using PersonalFinance.ApiService.Features.SavingsGoals.Validators;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetPlanner;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.SavingsGoals;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Every money endpoint rejects a currency code that is not in <see cref="SupportedCurrencies"/> under its Feature's
/// key, and accepts a supported code or none (localization spec §5, ADR 0016). Each request is otherwise valid, so the
/// currency is the only rule a rejection can come from.
/// </summary>
public class CurrencyCodeValidationTests
{
    /// <summary>A code the request carries, and whether it is accepted.</summary>
    public static TheoryData<string?, bool> Codes => new()
    {
        { null, true },
        { "USD", true },
        { "VND", true },
        { "EUR", false },
        { "usd", false },
        { "", false },
        { "US", false },
        { "USDX", false }
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void BudgetTracker_Transaction(string? code, bool accepted) =>
        AssertCurrencyRule(new SaveTransactionRequestValidator(),
            new SaveTransactionRequest
            {
                Date = new DateOnly(2026, 10, 1), Amount = 12.5m, Description = "Lunch", CategoryId = 1, CurrencyCode = code
            },
            ApiErrorKeys.BudgetTracker.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void BudgetTracker_PlannedAmount(string? code, bool accepted) =>
        AssertCurrencyRule(new SetPlannedRequestValidator(), new SetPlannedRequest { Amount = 300m, CurrencyCode = code },
            ApiErrorKeys.BudgetTracker.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void BudgetTracker_ActualOverride(string? code, bool accepted) =>
        AssertCurrencyRule(new SetActualOverrideRequestValidator(),
            new SetActualOverrideRequest { Amount = 300m, CurrencyCode = code },
            ApiErrorKeys.BudgetTracker.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void BudgetPlanner_Allocation(string? code, bool accepted) =>
        AssertCurrencyRule(new SaveBudgetRequestValidator(),
            new SaveBudgetRequestDto
            {
                Period = "2026-10", MonthlyIncome = 5000m, CurrencyCode = code,
                Allocations = new BudgetAllocationsDto { Housing = 1500m, Food = 600m }
            },
            ApiErrorKeys.BudgetPlanner.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void MoneyPriorities_Walk(string? code, bool accepted) =>
        AssertCurrencyRule(new SubmitPriorityWalkRequestValidator(),
            new SubmitPriorityWalkRequest { MonthlyAmount = 500m, DebtApr = 22m, DebtBalance = 5000m, CurrencyCode = code },
            ApiErrorKeys.MoneyPriorities.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void SavingsGoals_Goal(string? code, bool accepted) =>
        AssertCurrencyRule(new CreateSavingsGoalRequestValidator(),
            new CreateSavingsGoalRequestDto { Name = "Trip", TargetAmount = 2000m, CurrencyCode = code },
            ApiErrorKeys.SavingsGoals.CurrencyUnsupported, accepted);

    [Theory]
    [MemberData(nameof(Codes))]
    public void NetWorth_Parameters(string? code, bool accepted)
    {
        var request = new NetWorthProjectionRequestDto
        {
            StartingBalance = 1000m, AnnualSavings = 1000m, AnnualIncome = 50_000m, AnnualSpending = 30_000m,
            AggressiveSavings = 2000m, CurrentAge = 30, RetirementAge = 65, BaseRoi = 7m, ConservativeRoi = 4m,
            AggressiveRoi = 10m, CurrencyCode = code
        };

        ApiErrorEntryDto? failure = NetWorthProjectionValidator.Validate(request);

        if (accepted)
        {
            Assert.Null(failure);
        }
        else
        {
            Assert.Equal(ApiErrorKeys.NetWorth.CurrencyUnsupported, failure?.Key);
            Assert.Equal(nameof(NetWorthProjectionRequestDto.CurrencyCode), failure?.Field);
        }
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("vnd")]
    [InlineData("")]
    public async Task GoogleDrive_Connect_RejectsAnUnsupportedCode_BeforeTouchingTheDatabase(string code)
    {
        var service = new DriveConnectionService(new HttpClient(), new ConfigurationBuilder().Build(), null!,
            NullLogger<DriveConnectionService>.Instance);

        ApiErrorException rejected = await Assert.ThrowsAsync<ApiErrorException>(() => service.ConnectDriveAsync("user-1",
            new ConnectGoogleDriveRequestDto
            {
                FolderUrl = "https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUv", CurrencyCode = code
            }));

        Assert.Equal(ApiErrorKeys.GoogleDrive.CurrencyUnsupported, rejected.ToDto().Errors[0].Key);
    }

    private static void AssertCurrencyRule<T>(IValidator<T> validator, T request, string key, bool accepted)
    {
        List<string> codes = validator.Validate(request).Errors.Select(error => error.ErrorCode).ToList();

        if (accepted)
        {
            Assert.Empty(codes);
        }
        else
        {
            Assert.Equal([key], codes);
        }
    }
}
