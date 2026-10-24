namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's fund returns dataset: each Tracked Fund's monthly NAV total returns from its public Form N-PORT
/// reports (Item B.5: percent, distributions reinvested, net of expenses; 1.5 is 1.5%), and, per 1-, 3- and 5-year
/// window, figures computed from them in the refresh so the browser only selects them. Returns are month-end NAV
/// returns, not market prices, and lag by about a quarter.
/// </summary>
public sealed class FundReturnsSnapshotDto
{
    /// <summary>The last day of the latest month any fund has a return for ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>Each fund's monthly returns, in the configured order of the Tracked Funds that have any.</summary>
    public List<FundReturnSeriesDto> Funds { get; set; } = [];

    /// <summary>The computed figures for each window, shortest first.</summary>
    public List<FundReturnsWindowDto> Windows { get; set; } = [];
}

/// <summary>
/// One fund's monthly returns, one per calendar month from <see cref="FirstMonth"/> to <see cref="LastMonth"/>.
/// </summary>
public sealed class FundReturnSeriesDto
{
    /// <summary>The fund's ticker, matching a Tracked Fund of the funds dataset.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The first day of the month of the first return.</summary>
    public DateOnly FirstMonth { get; set; }

    /// <summary>The first day of the month of the latest return.</summary>
    public DateOnly LastMonth { get; set; }

    /// <summary>The return of each month in turn, in percent; null for a month no report gives.</summary>
    public List<decimal?> Returns { get; set; } = [];
}

/// <summary>
/// The figures for one window: each fund's over its latest months, and each pair's correlation.
/// </summary>
public sealed class FundReturnsWindowDto
{
    /// <summary>The window's length in years (1, 3 or 5).</summary>
    public int Years { get; set; }

    /// <summary>Each fund with a return for every month of its window, in the order of <see cref="FundReturnsSnapshotDto.Funds"/>.</summary>
    public List<FundWindowMetricsDto> Funds { get; set; } = [];

    /// <summary>Each pair of funds with returns for every month of their common window.</summary>
    public List<FundCorrelationDto> Correlations { get; set; } = [];
}

/// <summary>
/// One fund's figures over a window: the window's months ending with the fund's latest return.
/// </summary>
public sealed class FundWindowMetricsDto
{
    /// <summary>The fund's ticker.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The first day of the window's first month.</summary>
    public DateOnly Start { get; set; }

    /// <summary>The first day of the window's last month.</summary>
    public DateOnly End { get; set; }

    /// <summary>The growth over the window, in percent: the monthly returns compounded.</summary>
    public decimal TotalReturn { get; set; }

    /// <summary>The compound annual growth over the window, in percent a year.</summary>
    public decimal AnnualizedReturn { get; set; }

    /// <summary>The standard deviation of the monthly returns times √12, in percent a year.</summary>
    public decimal Volatility { get; set; }

    /// <summary>
    /// The annualized return less <see cref="TreasuryBill"/>, divided by <see cref="Volatility"/>; null when the
    /// Treasury bill rate is not known for the window or the volatility is zero.
    /// </summary>
    public decimal? Sharpe { get; set; }

    /// <summary>The average 3-month Treasury bill rate over the window, in percent a year; null when not known.</summary>
    public decimal? TreasuryBill { get; set; }

    /// <summary>The largest fall from a high within the window, at month ends, in percent (zero or negative).</summary>
    public decimal WorstFall { get; set; }
}

/// <summary>
/// How closely two funds' monthly returns moved together over a window.
/// </summary>
public sealed class FundCorrelationDto
{
    /// <summary>The first fund: its index in <see cref="FundReturnsSnapshotDto.Funds"/>.</summary>
    public int A { get; set; }

    /// <summary>The second fund: its index in <see cref="FundReturnsSnapshotDto.Funds"/> (higher than <see cref="A"/>).</summary>
    public int B { get; set; }

    /// <summary>The Pearson correlation of the two funds' monthly returns, from -1 to 1.</summary>
    public decimal Correlation { get; set; }

    /// <summary>The first day of the window's last month: the earlier of the two funds' latest months.</summary>
    public DateOnly End { get; set; }
}
