namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's fund facts dataset: each Tracked Fund's net assets, from its latest Form N-PORT report, and its
/// expense ratio, from the XBRL of its latest tailored shareholder report (Form N-CSR or N-CSRS). A fund that files no
/// such report with XBRL (SPY, a unit investment trust) has no expense ratio here.
/// </summary>
public sealed class FundFactsSnapshotDto
{
    /// <summary>The latest date any fact is as of ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>Each Tracked Fund, in the configured order.</summary>
    public List<FundFactsDto> Funds { get; set; } = [];
}

/// <summary>
/// One fund's facts; a fact the sources don't give is null.
/// </summary>
public sealed class FundFactsDto
{
    /// <summary>The fund's ticker.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The fund's net assets in US dollars, from its latest N-PORT report.</summary>
    public decimal? NetAssets { get; set; }

    /// <summary>The date the net assets are as of (that report's date).</summary>
    public DateOnly? NetAssetsAsOf { get; set; }

    /// <summary>The expense ratio of the share class matching the ticker, in percent (0.03 is 0.03%).</summary>
    public decimal? ExpenseRatio { get; set; }

    /// <summary>The last day of the period the expense ratio covers.</summary>
    public DateOnly? ExpenseRatioAsOf { get; set; }

    /// <summary>
    /// The EDGAR accession number of the newest shareholder report read for the fund, so a refresh reads only a newer
    /// one; null when the fund has none.
    /// </summary>
    public string? ShareholderReport { get; set; }
}
