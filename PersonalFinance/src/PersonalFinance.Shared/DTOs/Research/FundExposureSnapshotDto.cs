namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's fund exposure dataset: for each Tracked Fund's latest Form N-PORT report, the share of its net
/// assets by issuer country (where each issuer is organized, as the fund reports it; not where companies earn revenue)
/// and by the currency each holding is priced in (ADRs count as US dollars). Totals come from every holding, not only
/// the largest, with securities-lending cash collateral left out; what has no country or currency is "unallocated".
/// </summary>
public sealed class FundExposureSnapshotDto
{
    /// <summary>The latest report date of any fund ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>Each fund with a readable latest report, in the configured order.</summary>
    public List<FundExposureDto> Funds { get; set; } = [];
}

/// <summary>
/// One fund's exposure, from its latest report.
/// </summary>
public sealed class FundExposureDto
{
    /// <summary>The fund's ticker.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The date the report is as of.</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>The report's EDGAR accession number, so a refresh knows when a fund has filed a newer one.</summary>
    public string Accession { get; set; } = string.Empty;

    /// <summary>Each issuer country's share of net assets, largest first.</summary>
    public List<CountryShareDto> Countries { get; set; } = [];

    /// <summary>Each currency's share of net assets, largest first.</summary>
    public List<CurrencyShareDto> Currencies { get; set; } = [];

    /// <summary>The share with no country (cash not in the holdings, derivatives reported N/A), in percent.</summary>
    public decimal UnallocatedCountry { get; set; }

    /// <summary>The share with no currency, in percent.</summary>
    public decimal UnallocatedCurrency { get; set; }
}

/// <summary>
/// One issuer country's share of a fund.
/// </summary>
public sealed class CountryShareDto
{
    /// <summary>The ISO 3166-1 alpha-2 code as the fund reports it, e.g. <c>HK</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The three-digit numeric code the world map's shapes use, e.g. <c>344</c>; null when not in the table.</summary>
    public string? MapId { get; set; }

    /// <summary>The region it counts toward: North America, Latin America, Europe, Asia Pacific, or Middle East &amp; Africa.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }
}

/// <summary>
/// One currency's share of a fund.
/// </summary>
public sealed class CurrencyShareDto
{
    /// <summary>The ISO 4217 code, e.g. <c>USD</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }
}
