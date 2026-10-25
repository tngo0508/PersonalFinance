using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// The facts a Purpose Note may be written from (spec #315, "Purpose Note"): the Purpose, the answers on the path, the
/// "Your path" sentences, the leaf and its computed figures, nothing else. Dollar figures are rounded once here, to
/// whole dollars with <see cref="DisplayRounding.WholeDollars"/>; nothing is computed.
/// </summary>
internal static class PurposeNoteFacts
{
    public static PurposeNoteRequestDto For(string purpose, PriorityAnswers answers, IReadOnlyList<string> pathSentences,
        string leafLabel, PriorityLeafFigures figures) => new()
    {
        Purpose = purpose,
        Answers = AnswerFacts(answers).ToList(),
        PathLabels = pathSentences.ToList(),
        LeafLabel = leafLabel,
        Figures = FigureFacts(figures).ToList()
    };

    private static IEnumerable<PurposeNoteFactDto> AnswerFacts(PriorityAnswers answers)
    {
        yield return Fact("Extra money each month", Dollars(answers.MonthlyAmount));
        yield return Fact("Highest debt APR", Percent(answers.DebtApr));
        if (answers.DebtBalance is { } balance)
        {
            yield return Fact("Debt balance", Dollars(balance));
        }

        if (answers.EmergencyFundMonths is { } months)
        {
            yield return Fact("Months of expenses the emergency fund covers", Number(months));
        }

        if (answers.MonthlyExpenses is { } expenses)
        {
            yield return Fact("Monthly expenses", Dollars(expenses));
        }

        if (answers.HasFullEmployerMatch is { } fullMatch)
        {
            yield return Fact("Getting the full employer match", YesNo(fullMatch));
        }

        if (answers.EmployerMatchPercent is { } matchPercent)
        {
            yield return Fact("Employer match", $"{Percent(matchPercent)} of pay");
        }

        if (answers.HorizonYears is { } years)
        {
            yield return Fact("Years until the money is needed", Number(years));
        }

        if (answers.AgeBand is { } band)
        {
            yield return Fact("Age range", AgeRange(band));
        }

        if (answers.AtContributionLimit is { } atLimit)
        {
            yield return Fact("Already contributing the IRS limit for their age", YesNo(atLimit));
        }
    }

    private static IEnumerable<PurposeNoteFactDto> FigureFacts(PriorityLeafFigures figures)
    {
        if (figures.Debt is { } debt)
        {
            yield return Fact("Assumed investment return the APR is compared against", Percent(debt.AssumedReturnPercent));
            yield return Fact("Interest in the first month", Dollars(debt.Payoff.FirstMonthInterest));
            switch (debt.Payoff.Outcome)
            {
                case DebtPayoffOutcome.PaidOff:
                    yield return Fact("Months to pay it off", Number(debt.Payoff.Months ?? 0));
                    yield return Fact("Total interest", Dollars(debt.Payoff.TotalInterest ?? 0m));
                    break;
                case DebtPayoffOutcome.AmountDoesNotCoverInterest:
                    yield return Fact("Payoff", "the monthly amount does not cover the monthly interest");
                    break;
                case DebtPayoffOutcome.LongerThanMaximum:
                    yield return Fact("Payoff", $"takes longer than {DebtPayoff.MaxMonths / 12} years");
                    break;
            }
        }

        if (figures.EmergencyFund is { } fund)
        {
            yield return Fact($"Emergency fund target ({Number(fund.TargetMonths)} months)", Dollars(fund.TargetAmount));
            yield return Fact("Gap to the target", Dollars(fund.Gap));
            yield return Fact("Months to close the gap", Number(fund.MonthsToClose));
        }

        if (figures.EmployerMatch is { } match)
        {
            yield return Fact("Employer match", Percent(match.MatchPercent));
            yield return Fact("Up to this much match a year", Dollars(match.YearlyMatch));
        }

        if (figures.RetirementSplit is { } split)
        {
            yield return Fact("Yearly amount", Dollars(split.YearlyAmount));
            yield return Fact($"To the IRA (limit {Dollars(split.IraLimit)})", Dollars(split.ToIra));
            yield return Fact($"To the 401(k) (limit {Dollars(split.EmployeeDeferralLimit)})", Dollars(split.ToEmployeePlan));
            yield return Fact("To a taxable account", Dollars(split.ToTaxable));
            yield return Fact("IRS limits tax year", Number(split.TaxYear));
        }

        foreach (ScenarioBalance scenario in figures.Scenarios)
        {
            yield return Fact(
                $"{scenario.Scenario} balance after {Number(figures.HorizonYears ?? 0)} years at {Percent(scenario.RatePercent)} a year",
                Dollars(scenario.Balance));
        }

        if (figures.TotalContributed is { } contributed)
        {
            yield return Fact("Total contributed", Dollars(contributed));
        }
    }

    private static PurposeNoteFactDto Fact(string label, string value) => new() { Label = label, Value = value };

    private static string Dollars(decimal amount) =>
        "$" + DisplayRounding.WholeDollars(amount).ToString("N0", AppCulture.Default);

    private static string Percent(decimal percent) => percent.ToString("0.##", AppCulture.Default) + "%";

    private static string Number(decimal value) => value.ToString("0.#", AppCulture.Default);

    private static string Number(int value) => value.ToString(AppCulture.Default);

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string AgeRange(RetirementAgeBand band) => band switch
    {
        RetirementAgeBand.Under50 => "under 50",
        RetirementAgeBand.Age50To59Or64Up => "50 to 59, or 64 and over",
        RetirementAgeBand.Age60To63 => "60 to 63",
        _ => throw new InvalidOperationException($"Unknown age band '{band}'.")
    };
}
