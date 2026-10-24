namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's macro dataset, monthly: consumer prices from the US Bureau of Labor Statistics (CPI-U) and two
/// interest rates from the Federal Reserve Board's H.15 release. The Performance tab deflates fund returns by the CPI
/// and shows the rates beside them.
/// </summary>
public sealed class MacroSnapshotDto
{
    /// <summary>The last day of the latest month any series has a value for ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>Each month in turn, oldest first, with no month left out.</summary>
    public List<MacroMonthDto> Months { get; set; } = [];

    /// <summary>
    /// Where the Treasury bill rate came from: "H.15" (the Fed Board), or "Treasury" when the Fed's release could not
    /// be read and the Treasury's daily bill rates were averaged instead.
    /// </summary>
    public string TreasuryBillSource { get; set; } = "H.15";
}

/// <summary>
/// One month of the macro dataset; a series with no value for the month is null.
/// </summary>
public sealed class MacroMonthDto
{
    /// <summary>The first day of the month.</summary>
    public DateOnly Month { get; set; }

    /// <summary>CPI-U, all items, US city average, not seasonally adjusted (1982-84 = 100).</summary>
    public decimal? ConsumerPrices { get; set; }

    /// <summary>The effective federal funds rate, monthly average, in percent a year.</summary>
    public decimal? FedFunds { get; set; }

    /// <summary>The 3-month Treasury bill rate, monthly average, in percent a year.</summary>
    public decimal? TreasuryBill { get; set; }
}
