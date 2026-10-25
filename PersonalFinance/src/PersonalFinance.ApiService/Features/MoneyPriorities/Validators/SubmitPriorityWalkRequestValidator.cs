using FluentValidation;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Constants;
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
    public SubmitPriorityWalkRequestValidator()
    {
        RuleFor(x => x.MonthlyAmount)
            .NotNull().WithMessage(MoneyPrioritiesMessages.MonthlyAmountRequired)
            .GreaterThan(0m).WithMessage(MoneyPrioritiesMessages.MonthlyAmountRange)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.MonthlyAmountMax).WithMessage(MoneyPrioritiesMessages.MonthlyAmountRange)
            .PrecisionScale(18, 2, true).WithMessage(MoneyPrioritiesMessages.WholeCents);

        RuleFor(x => x.Purpose)
            .MaximumLength(MoneyPrioritiesLimits.PurposeMaxLength).WithMessage(MoneyPrioritiesMessages.PurposeTooLong);

        RuleFor(x => x.DebtApr)
            .NotNull().WithMessage(MoneyPrioritiesMessages.DebtAprRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.DebtAprMax).WithMessage(MoneyPrioritiesMessages.DebtAprRange)
            .PrecisionScale(5, 2, true).WithMessage(MoneyPrioritiesMessages.DebtAprRange);

        RuleFor(x => x.DebtBalance)
            .NotNull().WithMessage(MoneyPrioritiesMessages.DebtBalanceRequired)
            .GreaterThan(0m).WithMessage(MoneyPrioritiesMessages.DebtBalanceRange)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.DebtBalanceMax).WithMessage(MoneyPrioritiesMessages.DebtBalanceRange)
            .PrecisionScale(18, 2, true).WithMessage(MoneyPrioritiesMessages.WholeCents)
            .When(AsksDebtBalance);

        RuleFor(x => x.EmergencyFundMonths)
            .NotNull().WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.EmergencyFundMonthsMax)
            .WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRange)
            .PrecisionScale(4, 1, true).WithMessage(MoneyPrioritiesMessages.EmergencyFundMonthsRange)
            .When(AsksEmergencyFund);

        RuleFor(x => x.MonthlyExpenses)
            .NotNull().WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRequired)
            .GreaterThan(0m).WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRange)
            .LessThanOrEqualTo(MoneyPrioritiesLimits.MonthlyExpensesMax).WithMessage(MoneyPrioritiesMessages.MonthlyExpensesRange)
            .PrecisionScale(18, 2, true).WithMessage(MoneyPrioritiesMessages.WholeCents)
            .When(AsksMonthlyExpenses);

        RuleFor(x => x.HasFullEmployerMatch)
            .NotNull().WithMessage(MoneyPrioritiesMessages.HasFullEmployerMatchRequired)
            .When(AsksEmployerMatch);

        RuleFor(x => x.EmployerMatchPercent)
            .NotNull().WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRequired)
            .InclusiveBetween(0m, MoneyPrioritiesLimits.EmployerMatchPercentMax)
            .WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRange)
            .PrecisionScale(5, 2, true).WithMessage(MoneyPrioritiesMessages.EmployerMatchPercentRange)
            .When(AsksEmployerMatchPercent);

        RuleFor(x => x.HorizonYears)
            .NotNull().WithMessage(MoneyPrioritiesMessages.HorizonYearsRequired)
            .InclusiveBetween(MoneyPrioritiesLimits.HorizonYearsMin, MoneyPrioritiesLimits.HorizonYearsMax)
            .WithMessage(MoneyPrioritiesMessages.HorizonYearsRange)
            .When(AsksHorizon);

        RuleFor(x => x.AgeBand)
            .NotNull().WithMessage(MoneyPrioritiesMessages.AgeBandRequired)
            .IsInEnum().WithMessage(MoneyPrioritiesMessages.AgeBandRequired)
            .When(AsksContributionLimit);

        RuleFor(x => x.AtContributionLimit)
            .NotNull().WithMessage(MoneyPrioritiesMessages.AtContributionLimitRequired)
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
