using System.Globalization;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Web.Services;

/// <summary>
///     Builds the FI Breakdown cards from a Net Worth projection. All wording and number formatting lives here, so the
///     server-rendered panel and the live updates after each recalculation show exactly the same text.
/// </summary>
public static class FiBreakdownBuilder
{
    private const string NotAvailable = "—";
    private const string NeedsSpending = "Set your annual spending to see this.";
    private const string NeedsIncome = "Add your annual income to see this.";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static FiBreakdownViewModel Build(NetWorthProjectionDto projection)
    {
        return new FiBreakdownViewModel
        {
            Cards =
            [
                SavingsRate(projection),
                FiTarget(projection),
                ProgressToFi(projection),
                FiAge(projection)
            ]
        };
    }

    private static FiBreakdownCardViewModel SavingsRate(NetWorthProjectionDto p)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "savings-rate",
            Label = "Savings Rate",
            Inputs = [Input("Annual Savings", Money(p.AnnualSavings)), Input("Annual Income", Money(p.AnnualIncome))]
        };

        if (p.SavingsRatePercent is not { } rate)
        {
            return Unavailable(card, NeedsIncome);
        }

        card.Value = Percent(rate);
        card.Formula = $"{Money(p.AnnualSavings)} ÷ {Money(p.AnnualIncome)} = {card.Value}";
        return card;
    }

    private static FiBreakdownCardViewModel FiTarget(NetWorthProjectionDto p)
    {
        return new FiBreakdownCardViewModel
        {
            Key = "fi-target",
            Label = "FI Target",
            Value = Money(p.FiTarget),
            Inputs = [Input("Annual Spending", Money(p.AnnualSpending))],
            Formula = $"{Money(p.AnnualSpending)} × 25 = {Money(p.FiTarget)}"
        };
    }

    private static FiBreakdownCardViewModel ProgressToFi(NetWorthProjectionDto p)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "progress",
            Label = "Progress to FI",
            Inputs = [Input("Starting Balance", Money(p.StartingBalance)), Input("FI Target", Money(p.FiTarget))]
        };

        if (p.ProgressToFiPercent is not { } progress)
        {
            return Unavailable(card, NeedsSpending);
        }

        card.Value = Percent(progress);
        card.Formula = $"{Money(p.StartingBalance)} ÷ {Money(p.FiTarget)} = {card.Value}";
        return card;
    }

    private static FiBreakdownCardViewModel FiAge(NetWorthProjectionDto p)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "fi-age",
            Label = "FI age",
            Inputs =
            [
                Input("Starting Balance", Money(p.StartingBalance)),
                Input("Annual Savings", Money(p.AnnualSavings)),
                Input("Base return", Rate(p.Base.RoiPercent)),
                Input("Current Age", p.CurrentAge.ToString(Invariant)),
                Input("Retirement Age", p.RetirementAge.ToString(Invariant)),
                Input("FI Target", Money(p.FiTarget))
            ]
        };

        if (p.FiTarget <= 0m)
        {
            return Unavailable(card, NeedsSpending);
        }

        card.Value = p.Base.FiReachedAge is { } age
            ? $"{age} ({YearsFromNow(age - p.CurrentAge)})"
            : NotReached(p, capitalized: true);
        card.Formula = $"Grow {Money(p.StartingBalance)} at {Rate(p.Base.RoiPercent)} a year, adding " +
                       $"{Money(p.AnnualSavings)} a year, until it reaches {Money(p.FiTarget)}.";
        card.Scenarios =
        [
            Scenario(p, p.Base, showSavings: false),
            Scenario(p, p.Conservative, showSavings: false),
            // Aggressive uses its own savings amount, so it is named.
            Scenario(p, p.Aggressive, showSavings: true)
        ];
        return card;
    }

    private static string Scenario(NetWorthProjectionDto p, NetWorthScenarioDto scenario, bool showSavings)
    {
        string assumptions = showSavings
            ? $"{Rate(scenario.RoiPercent)}, {Money(scenario.AnnualSavings)} a year"
            : Rate(scenario.RoiPercent);
        string outcome = scenario.FiReachedAge is { } age ? $"age {age}" : NotReached(p, capitalized: false);
        return $"{scenario.Name} ({assumptions}): {outcome}";
    }

    private static string YearsFromNow(int years)
    {
        return years switch
        {
            <= 0 => "this year",
            1 => "in 1 year",
            _ => $"in {years} years"
        };
    }

    private static string NotReached(NetWorthProjectionDto p, bool capitalized)
    {
        return $"{(capitalized ? "Not" : "not")} reached by age {p.RetirementAge}";
    }

    private static FiBreakdownCardViewModel Unavailable(FiBreakdownCardViewModel card, string note)
    {
        card.Value = NotAvailable;
        card.Note = note;
        return card;
    }

    private static FiBreakdownInputViewModel Input(string label, string value)
    {
        return new FiBreakdownInputViewModel { Label = label, Value = value };
    }

    private static string Money(decimal amount)
    {
        return "$" + Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", Invariant);
    }

    private static string Percent(decimal percent)
    {
        return Math.Round(percent, 1, MidpointRounding.AwayFromZero).ToString("F1", Invariant) + "%";
    }

    private static string Rate(decimal ratePercent)
    {
        return ratePercent.ToString("0.##", Invariant) + "%";
    }
}
