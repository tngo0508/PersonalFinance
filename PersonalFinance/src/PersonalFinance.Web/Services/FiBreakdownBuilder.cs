using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Networth;

namespace PersonalFinance.Web.Services;

/// <summary>
///     Builds the FI Breakdown cards from a Net Worth projection. All wording and number formatting lives here, so the
///     server-rendered panel and the live updates after each recalculation show exactly the same text. The wording
///     follows the UI language (<c>NetWorth.Breakdown.*</c> keys) and the amounts the request culture, through
///     <see cref="MoneyFormatter"/> (localization spec §2.1, §2.2), in the projection's currency (ADR 0016).
/// </summary>
public static class FiBreakdownBuilder
{
    private const string NotAvailable = "—";

    public static FiBreakdownViewModel Build(NetWorthProjectionDto projection, IStringLocalizer localizer)
    {
        return new FiBreakdownViewModel
        {
            Cards =
            [
                SavingsRate(projection, localizer),
                FiTarget(projection, localizer),
                ProgressToFi(projection, localizer),
                FiAge(projection, localizer)
            ]
        };
    }

    private static FiBreakdownCardViewModel SavingsRate(NetWorthProjectionDto p, IStringLocalizer localizer)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "savings-rate",
            Label = localizer["NetWorth.Field.SavingsRate"],
            Inputs =
            [
                Input(localizer["NetWorth.Field.AnnualSavings"], Money(p.AnnualSavings, p.CurrencyCode)),
                Input(localizer["NetWorth.Field.AnnualIncome"], Money(p.AnnualIncome, p.CurrencyCode))
            ]
        };

        if (p.SavingsRatePercent is not { } rate)
        {
            return Unavailable(card, localizer["NetWorth.Breakdown.NeedsIncome"]);
        }

        card.Value = Percent(rate);
        card.Formula = $"{Money(p.AnnualSavings, p.CurrencyCode)} ÷ {Money(p.AnnualIncome, p.CurrencyCode)} = {card.Value}";
        return card;
    }

    private static FiBreakdownCardViewModel FiTarget(NetWorthProjectionDto p, IStringLocalizer localizer)
    {
        return new FiBreakdownCardViewModel
        {
            Key = "fi-target",
            Label = localizer["NetWorth.Js.FiTarget"],
            Value = Money(p.FiTarget, p.CurrencyCode),
            Inputs = [Input(localizer["NetWorth.Field.AnnualSpending"], Money(p.AnnualSpending, p.CurrencyCode))],
            Formula = $"{Money(p.AnnualSpending, p.CurrencyCode)} × 25 = {Money(p.FiTarget, p.CurrencyCode)}"
        };
    }

    private static FiBreakdownCardViewModel ProgressToFi(NetWorthProjectionDto p, IStringLocalizer localizer)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "progress",
            Label = localizer["NetWorth.Breakdown.Progress"],
            Inputs =
            [
                Input(localizer["NetWorth.Field.StartingBalance"], Money(p.StartingBalance, p.CurrencyCode)),
                Input(localizer["NetWorth.Js.FiTarget"], Money(p.FiTarget, p.CurrencyCode))
            ]
        };

        if (p.ProgressToFiPercent is not { } progress)
        {
            return Unavailable(card, localizer["NetWorth.Breakdown.NeedsSpending"]);
        }

        card.Value = Percent(progress);
        card.Formula = $"{Money(p.StartingBalance, p.CurrencyCode)} ÷ {Money(p.FiTarget, p.CurrencyCode)} = {card.Value}";
        return card;
    }

    private static FiBreakdownCardViewModel FiAge(NetWorthProjectionDto p, IStringLocalizer localizer)
    {
        var card = new FiBreakdownCardViewModel
        {
            Key = "fi-age",
            Label = localizer["NetWorth.Breakdown.FiAge"],
            Inputs =
            [
                Input(localizer["NetWorth.Field.StartingBalance"], Money(p.StartingBalance, p.CurrencyCode)),
                Input(localizer["NetWorth.Field.AnnualSavings"], Money(p.AnnualSavings, p.CurrencyCode)),
                Input(localizer["NetWorth.Breakdown.BaseReturn"], Rate(p.Base.RoiPercent)),
                Input(localizer["NetWorth.Field.CurrentAge"], p.CurrentAge.ToString(CultureInfo.CurrentCulture)),
                Input(localizer["NetWorth.Field.RetirementAge"], p.RetirementAge.ToString(CultureInfo.CurrentCulture)),
                Input(localizer["NetWorth.Js.FiTarget"], Money(p.FiTarget, p.CurrencyCode))
            ]
        };

        if (p.FiTarget <= 0m)
        {
            return Unavailable(card, localizer["NetWorth.Breakdown.NeedsSpending"]);
        }

        card.Value = p.Base.FiReachedAge is { } age
            ? localizer.Named("NetWorth.Breakdown.AgeWhen", ("age", age), ("when", YearsFromNow(age - p.CurrentAge, localizer)))
            : localizer.Named("NetWorth.Breakdown.NotReached", ("age", p.RetirementAge));
        card.Formula = localizer.Named("NetWorth.Breakdown.FiAgeFormula", ("balance", Money(p.StartingBalance, p.CurrencyCode)),
            ("rate", Rate(p.Base.RoiPercent)), ("savings", Money(p.AnnualSavings, p.CurrencyCode)), ("target", Money(p.FiTarget, p.CurrencyCode)));
        card.Scenarios =
        [
            Scenario(p, p.Base, showSavings: false, localizer),
            Scenario(p, p.Conservative, showSavings: false, localizer),
            // Aggressive uses its own savings amount, so it is named.
            Scenario(p, p.Aggressive, showSavings: true, localizer)
        ];
        return card;
    }

    private static string Scenario(NetWorthProjectionDto p, NetWorthScenarioDto scenario, bool showSavings, IStringLocalizer localizer)
    {
        string assumptions = showSavings
            ? localizer.Named("NetWorth.Breakdown.RateAndSavings", ("rate", Rate(scenario.RoiPercent)),
                ("savings", Money(scenario.AnnualSavings, p.CurrencyCode)))
            : Rate(scenario.RoiPercent);
        string outcome = scenario.FiReachedAge is { } age
            ? localizer.Named("NetWorth.Breakdown.ReachedAge", ("age", age))
            : localizer.Named("NetWorth.Breakdown.NotReachedInline", ("age", p.RetirementAge));
        return localizer.Named("NetWorth.Breakdown.Scenario", ("name", ScenarioName(scenario.Name, localizer)),
            ("assumptions", assumptions), ("outcome", outcome));
    }

    /// <summary>The ApiService names the scenarios Base, Conservative and Aggressive; any other name shows as sent.</summary>
    private static string ScenarioName(string name, IStringLocalizer localizer)
    {
        return name is "Base" or "Conservative" or "Aggressive" ? localizer[$"NetWorth.Js.Scenario.{name}"] : name;
    }

    private static string YearsFromNow(int years, IStringLocalizer localizer)
    {
        return years <= 0 ? localizer["NetWorth.Breakdown.ThisYear"] : localizer.Plural("NetWorth.Breakdown.InYears", years);
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

    private static string Money(decimal amount, string currencyCode)
    {
        return MoneyFormatter.FormatWhole(amount, SupportedCurrencies.CodeOrDefault(currencyCode));
    }

    private static string Percent(decimal percent)
    {
        return Math.Round(percent, 1, MidpointRounding.AwayFromZero).ToString("F1", CultureInfo.CurrentCulture) + "%";
    }

    private static string Rate(decimal ratePercent)
    {
        return ratePercent.ToString("0.##", CultureInfo.CurrentCulture) + "%";
    }
}
