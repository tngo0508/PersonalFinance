using FluentValidation;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.MoneyPriorities;

namespace PersonalFinance.ApiService.Features.MoneyPriorities.Validators;

/// <summary>
/// Validator for a submitted Priority Walk: the ranges of the spec's column table (#315), and the follow-up rules. A
/// question or follow-up is required exactly when Priority Tree version 1 asks it on the path the earlier answers lead
/// to; an answer off that path is ignored, not rejected (the service stores it as null). The scale rules match the
/// column precision, so a value round-trips through the database unchanged.
/// </summary>
internal sealed class SubmitPriorityWalkRequestValidator : AbstractValidator<SubmitPriorityWalkRequest>
{
    // The {min} and {max} of each "over {min} and at most {max}" amount message.
    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> MonthlyAmountRangeArgs =
        ErrorArgs.MoneyRange(0m, MoneyPrioritiesLimits.MonthlyAmountMax, SupportedCurrencies.Default.Code);

    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> DebtBalanceRangeArgs =
        ErrorArgs.MoneyRange(0m, MoneyPrioritiesLimits.DebtBalanceMax, SupportedCurrencies.Default.Code);

    private static readonly IReadOnlyDictionary<string, ApiErrorArgDto> MonthlyExpensesRangeArgs =
        ErrorArgs.MoneyRange(0m, MoneyPrioritiesLimits.MonthlyExpensesMax, SupportedCurrencies.Default.Code);

    public SubmitPriorityWalkRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).SupportedCurrency(ApiErrorKeys.MoneyPriorities.CurrencyUnsupported);

        RuleFor(x => x.MonthlyAmount)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyAmountRequired)
                .WithMessage(MoneyPrioritiesMessages.MonthlyAmountRequired)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange)
                .WithMessage(MoneyPrioritiesMessages.MonthlyAmountRange)
                .WithState(_ => MonthlyAmountRangeArgs)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.MonthlyAmountMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyAmountRange)
                .WithMessage(MoneyPrioritiesMessages.MonthlyAmountRange)
                .WithState(_ => MonthlyAmountRangeArgs)
            .PrecisionScale(18, 2, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.WholeCents)
                .WithMessage(MoneyPrioritiesMessages.WholeCents);

        RuleFor(x => x.Purpose)
            .MaximumLength(MoneyPrioritiesLimits.PurposeMaxLength)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.PurposeTooLong)
                .WithMessage(MoneyPrioritiesMessages.PurposeTooLong);

        RuleFor(x => x.DebtApr)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtAprRequired)
                .WithMessage(MoneyPrioritiesMessages.DebtAprRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.DebtAprMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtAprRange)
                .WithMessage(MoneyPrioritiesMessages.DebtAprRange)
            .PrecisionScale(5, 2, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtAprRange)
                .WithMessage(MoneyPrioritiesMessages.DebtAprRange);

        RuleFor(x => x.DebtBalance)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtBalanceRequired)
                .WithMessage(MoneyPrioritiesMessages.DebtBalanceRequired)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtBalanceRange)
                .WithMessage(MoneyPrioritiesMessages.DebtBalanceRange)
                .WithState(_ => DebtBalanceRangeArgs)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.DebtBalanceMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.DebtBalanceRange)
                .WithMessage(MoneyPrioritiesMessages.DebtBalanceRange)
                .WithState(_ => DebtBalanceRangeArgs)
            .PrecisionScale(18, 2, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.WholeCents)
                .WithMessage(MoneyPrioritiesMessages.WholeCents)
            .When(AsksDebtBalance);

        RuleFor(x => x.EmergencyFundMonths)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmergencyFundMonthsRequired)
                .WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.EmergencyFundMonthsMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmergencyFundMonthsRange)
                .WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRange)
            .PrecisionScale(4, 1, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmergencyFundMonthsRange)
                .WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRange)
            .When(AsksEmergencyFund);

        RuleFor(x => x.MonthlyExpenses)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyExpensesRequired)
                .WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRequired)
            .GreaterThan(0m)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyExpensesRange)
                .WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRange)
                .WithState(_ => MonthlyExpensesRangeArgs)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.MonthlyExpensesMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.MonthlyExpensesRange)
                .WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRange)
                .WithState(_ => MonthlyExpensesRangeArgs)
            .PrecisionScale(18, 2, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.WholeCents)
                .WithMessage(MoneyPrioritiesMessages.WholeCents)
            .When(AsksMonthlyExpenses);

        RuleFor(x => x.HasFullEmployerMatch)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.HasFullEmployerMatchRequired)
                .WithMessage(MoneyPrioritiesMessages.HasFullEmployerMatchRequired)
            .When(AsksEmployerMatch);

        RuleFor(x => x.EmployerMatchPercent)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmployerMatchPercentRequired)
                .WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.EmployerMatchPercentMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmployerMatchPercentRange)
                .WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRange)
            .PrecisionScale(5, 2, true)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.EmployerMatchPercentRange)
                .WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRange)
            .When(AsksEmployerMatchPercent);

        RuleFor(x => x.HorizonYears)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.HorizonYearsRequired)
                .WithMessage(MoneyPrioritiesMessages.HorizonYearsRequired)
            .InclusiveBetween(MoneyPrioritiesLimits.HorizonYearsMin, MoneyPrioritiesLimits.HorizonYearsMax)
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.HorizonYearsRange)
                .WithMessage(MoneyPrioritiesMessages.HorizonYearsRange)
            .When(AsksHorizon);

        RuleFor(x => x.AgeBand)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.AgeBandRequired)
                .WithMessage(MoneyPrioritiesMessages.AgeBandRequired)
            .IsInEnum()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.AgeBandRequired)
                .WithMessage(MoneyPrioritiesMessages.AgeBandRequired)
            .When(AsksContributionLimit);

        RuleFor(x => x.AtContributionLimit)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.MoneyPriorities.AtContributionLimitRequired)
                .WithMessage(MoneyPrioritiesMessages.AtContributionLimitRequired)
            .When(AsksContributionLimit);
    }

    // Each condition asks the shared tree decisions (PriorityQuestions) which answers the path asks for, so the
    // validator and PriorityPathEvaluator cannot disagree.
    private static bool AsksDebtBalance(SubmitPriorityWalkRequest r) => Asked(r).DebtBalance;

    private static bool AsksEmergencyFund(SubmitPriorityWalkRequest r) => Asked(r).EmergencyFundMonths;

    private static bool AsksMonthlyExpenses(SubmitPriorityWalkRequest r) => Asked(r).MonthlyExpenses;

    private static bool AsksEmployerMatch(SubmitPriorityWalkRequest r) => Asked(r).HasFullEmployerMatch;

    private static bool AsksEmployerMatchPercent(SubmitPriorityWalkRequest r) => Asked(r).EmployerMatchPercent;

    private static bool AsksHorizon(SubmitPriorityWalkRequest r) => Asked(r).HorizonYears;

    private static bool AsksContributionLimit(SubmitPriorityWalkRequest r) => Asked(r).AtContributionLimit;

    /// <summary>
    /// The answers the path asks for, as far as the request's answers lead. Without a debt APR the path cannot leave
    /// Q1, so nothing further is asked. The monthly amount never changes the path, so a missing one is read as 0.
    /// </summary>
    private static PriorityAskedAnswers Asked(SubmitPriorityWalkRequest r)
    {
        if (r.DebtApr is not { } debtApr)
        {
            return PriorityQuestions.Asked([PriorityNodeIds.Start, PriorityNodeIds.HighInterestDebt]);
        }

        var answers = new PriorityAnswers
        {
            MonthlyAmount = r.MonthlyAmount ?? 0m,
            DebtApr = debtApr,
            EmergencyFundMonths = r.EmergencyFundMonths,
            HasFullEmployerMatch = r.HasFullEmployerMatch,
            HorizonYears = r.HorizonYears,
            AtContributionLimit = r.AtContributionLimit
        };
        return PriorityQuestions.Asked(PriorityQuestions.Walk(answers));
    }
}
