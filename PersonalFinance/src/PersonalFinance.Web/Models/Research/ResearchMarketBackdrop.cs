using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Home page's market backdrop: the latest month of each macro series, as the Basket and Fund pages'
/// growth charts use them (consumer prices to adjust for inflation, the Treasury bill rate behind the Sharpe ratio).
/// Each figure is null when its series has no month yet.
/// </summary>
/// <param name="FedFunds">The effective federal funds rate of its latest month, in percent a year, and that month.</param>
/// <param name="TreasuryBill">The 3-month Treasury bill rate of its latest month, in percent a year, and that month.</param>
/// <param name="Inflation">
/// How much consumer prices rose over the 12 months to the latest month with a CPI value, in percent, and that
/// month; null when the month a year earlier has no value.
/// </param>
public sealed record ResearchMarketBackdrop(
    ResearchMarketFigure? FedFunds,
    ResearchMarketFigure? TreasuryBill,
    ResearchMarketFigure? Inflation)
{
    /// <summary>True when no series has a figure, so the backdrop has nothing to show.</summary>
    public bool IsEmpty => FedFunds is null && TreasuryBill is null && Inflation is null;

    /// <summary>
    /// The backdrop of <paramref name="macro"/>: each series' latest month with a value, and the 12-month rise in
    /// consumer prices to the latest month that has one. Null when <paramref name="macro"/> is null.
    /// </summary>
    public static ResearchMarketBackdrop? From(MacroSnapshotDto? macro)
    {
        if (macro is null)
        {
            return null;
        }

        List<MacroMonthDto> months = macro.Months;
        MacroMonthDto? fedFunds = months.LastOrDefault(m => m.FedFunds is not null);
        MacroMonthDto? bill = months.LastOrDefault(m => m.TreasuryBill is not null);
        MacroMonthDto? prices = months.LastOrDefault(m => m.ConsumerPrices is not null);
        ResearchMarketFigure? inflation = null;
        if (prices?.ConsumerPrices is { } latest)
        {
            DateOnly yearEarlier = prices.Month.AddMonths(-12);
            decimal? earlier = months.FirstOrDefault(m => m.Month == yearEarlier)?.ConsumerPrices;
            if (earlier is > 0)
            {
                inflation = new ResearchMarketFigure(((latest / earlier.Value) - 1m) * 100m, prices.Month);
            }
        }

        return new ResearchMarketBackdrop(
            fedFunds is null ? null : new ResearchMarketFigure(fedFunds.FedFunds!.Value, fedFunds.Month),
            bill is null ? null : new ResearchMarketFigure(bill.TreasuryBill!.Value, bill.Month),
            inflation);
    }
}

/// <summary>One figure of the market backdrop: a percentage and the month it is for.</summary>
/// <param name="Percent">The figure in percent: 4.33 is 4.33%.</param>
/// <param name="Month">The first day of the month the figure is for.</param>
public sealed record ResearchMarketFigure(decimal Percent, DateOnly Month);
