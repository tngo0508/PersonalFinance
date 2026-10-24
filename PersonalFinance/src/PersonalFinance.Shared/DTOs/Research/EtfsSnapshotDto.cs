namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's ETFs dataset: per calendar quarter, what each of a few well-known ETFs held, read from their
/// Form N-PORT reports. Each fund's holdings are grouped by N-PORT asset category, with the 25 largest holdings of
/// each category listed and the rest summed into "Other". Percentages are of the fund's net assets (7.5 is 7.5%).
/// </summary>
public sealed class EtfsSnapshotDto
{
    /// <summary>The latest report date of any fund ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The quarter labels, oldest first, e.g. <c>2019 Q4</c>; <see cref="Values"/> follows this order.</summary>
    public List<string> Periods { get; set; } = [];

    /// <summary>The funds, in the configured order (the page opens on the first).</summary>
    public List<EtfFundDto> Funds { get; set; } = [];

    /// <summary>One entry per quarter, in the order of <see cref="Periods"/>.</summary>
    public List<EtfsPeriodDto> Values { get; set; } = [];
}

/// <summary>
/// One fund of the ETFs dataset.
/// </summary>
public sealed class EtfFundDto
{
    /// <summary>The ETF's ticker, e.g. <c>SPY</c>.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The ETF's name for display, e.g. <c>SPDR S&amp;P 500 ETF Trust</c>.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// One calendar quarter of the ETFs dataset: the report of each fund that filed one for the quarter.
/// </summary>
public sealed class EtfsPeriodDto
{
    /// <summary>The quarter label, e.g. <c>2026 Q2</c>.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The funds with a report for the quarter, in the order of <see cref="EtfsSnapshotDto.Funds"/>.</summary>
    public List<EtfHoldingsDto> Funds { get; set; } = [];
}

/// <summary>
/// What one fund held on one report date.
/// </summary>
public sealed class EtfHoldingsDto
{
    /// <summary>The fund's ticker, matching an entry of <see cref="EtfsSnapshotDto.Funds"/>.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The date the holdings are as of (the report's period date).</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>The fund's net assets (US$) on the report date.</summary>
    public decimal NetAssets { get; set; }

    /// <summary>How many holdings the report lists.</summary>
    public int HoldingCount { get; set; }

    /// <summary>The holdings by asset category, largest weight first.</summary>
    public List<EtfCategoryDto> Categories { get; set; } = [];
}

/// <summary>
/// One N-PORT asset category of a fund's report, with its largest holdings.
/// </summary>
public sealed class EtfCategoryDto
{
    /// <summary>The N-PORT asset category code, e.g. <c>EC</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The category for display, e.g. <c>Common equity</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The category's weight: the sum of <see cref="Top"/> and <see cref="OtherPercent"/>.</summary>
    public decimal Percent { get; set; }

    /// <summary>How many holdings the category has.</summary>
    public int HoldingCount { get; set; }

    /// <summary>The category's 25 largest holdings by % of net assets, largest first.</summary>
    public List<EtfHoldingDto> Top { get; set; } = [];

    /// <summary>How many holdings are summed into "Other" (0 when the category has 25 or fewer).</summary>
    public int OtherCount { get; set; }

    /// <summary>The summed % of net assets of the holdings outside <see cref="Top"/>.</summary>
    public decimal OtherPercent { get; set; }
}

/// <summary>
/// One holding of a fund.
/// </summary>
public sealed class EtfHoldingDto
{
    /// <summary>The issuer's name as the report gives it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The holding's value as a % of the fund's net assets; negative for a short position.</summary>
    public decimal Percent { get; set; }
}
