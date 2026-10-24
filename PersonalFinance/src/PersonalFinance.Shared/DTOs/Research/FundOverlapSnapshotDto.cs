namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's fund overlap dataset: for every pair of Tracked Funds, how much of their money is in the same
/// securities, as of each fund's latest public Form N-PORT report. Overlap is by weight: the sum, over the securities
/// both funds hold, of the smaller of the two weights (percentages of each fund's net assets; 7.5 is 7.5%). Each pair
/// lists its largest shared holdings by name. No security identifier (CUSIP or ISIN) is ever part of it (ADR 0010):
/// securities are matched in memory during the refresh, and each shared holding is referred to by its index in
/// <see cref="Holdings"/>.
/// </summary>
public sealed class FundOverlapSnapshotDto
{
    /// <summary>The latest report date of any fund ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The funds the overlap covers, in the configured order, each with the report it was computed from.</summary>
    public List<FundOverlapFundDto> Funds { get; set; } = [];

    /// <summary>The names of the shared holdings, each listed once and referred to by index.</summary>
    public List<string> Holdings { get; set; } = [];

    /// <summary>One entry per pair of <see cref="Funds"/> (first index lower than the second).</summary>
    public List<FundOverlapPairDto> Pairs { get; set; } = [];
}

/// <summary>
/// One fund of the overlap dataset, with the report its holdings were read from.
/// </summary>
public sealed class FundOverlapFundDto
{
    /// <summary>The fund's ticker, matching a Tracked Fund of the funds dataset.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The date the fund's holdings are as of (its latest report's period date).</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>The EDGAR accession number of that report, so a refresh knows when a fund has filed a newer one.</summary>
    public string Accession { get; set; } = string.Empty;
}

/// <summary>
/// The overlap of two funds.
/// </summary>
public sealed class FundOverlapPairDto
{
    /// <summary>The first fund: its index in <see cref="FundOverlapSnapshotDto.Funds"/>.</summary>
    public int A { get; set; }

    /// <summary>The second fund: its index in <see cref="FundOverlapSnapshotDto.Funds"/>.</summary>
    public int B { get; set; }

    /// <summary>The overlap by weight, in percent (0 to 100).</summary>
    public decimal Overlap { get; set; }

    /// <summary>
    /// The share of the first fund's net assets in securities the second fund also holds, in percent: how much of the
    /// first fund the second one covers.
    /// </summary>
    public decimal CoveredA { get; set; }

    /// <summary>The share of the second fund's net assets in securities the first fund also holds, in percent.</summary>
    public decimal CoveredB { get; set; }

    /// <summary>How many securities both funds hold.</summary>
    public int SharedCount { get; set; }

    /// <summary>The largest shared holdings, by the smaller of the two weights, largest first.</summary>
    public List<FundOverlapHoldingDto> Shared { get; set; } = [];
}

/// <summary>
/// One security both funds of a pair hold.
/// </summary>
public sealed class FundOverlapHoldingDto
{
    /// <summary>The holding's name: its index in <see cref="FundOverlapSnapshotDto.Holdings"/>.</summary>
    public int Holding { get; set; }

    /// <summary>Its weight in the first fund (% of net assets).</summary>
    public decimal WeightA { get; set; }

    /// <summary>Its weight in the second fund (% of net assets).</summary>
    public decimal WeightB { get; set; }
}
