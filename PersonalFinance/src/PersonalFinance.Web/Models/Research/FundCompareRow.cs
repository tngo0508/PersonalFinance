namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// One Tracked Fund's row of the Funds page's comparison table, over the page's window. Every figure comes from a
/// snapshot as it was precomputed (fund returns per window, fund facts, the funds snapshot's latest quarter); a figure
/// that has not loaded or was not filed is null. Percentages are in percent: 12.3 is 12.3%.
/// </summary>
public sealed class FundCompareRow
{
    /// <summary>The fund's ticker, e.g. <c>SPY</c>.</summary>
    public required string Ticker { get; init; }

    /// <summary>The fund's name, e.g. <c>SPDR S&amp;P 500 ETF Trust</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The fund's role among the Tracked Funds (e.g. "US broad"), or null when it has none.</summary>
    public string? Role { get; init; }

    /// <summary>The annualized return over the window, in percent; null when the returns have not loaded or are too short.</summary>
    public decimal? AnnualReturn { get; init; }

    /// <summary>The annualized volatility over the window, in percent; null like <see cref="AnnualReturn"/>.</summary>
    public decimal? Volatility { get; init; }

    /// <summary>The Sharpe ratio over the window; null when it could not be computed or the returns have not loaded.</summary>
    public decimal? Sharpe { get; init; }

    /// <summary>The expense ratio from the fund's latest shareholder report, in percent; null when not filed.</summary>
    public decimal? ExpenseRatio { get; init; }

    /// <summary>The fund's net assets in US dollars from its latest N-PORT report; 0 when it filed none.</summary>
    public decimal NetAssets { get; init; }
}
