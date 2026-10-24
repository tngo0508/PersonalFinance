namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's fund industries dataset, for the Sectors tab's basket view: for each Tracked Fund's latest Form
/// N-PORT report, its share of net assets by SEC industry office and SIC industry (over every holding, before any
/// trim) with the largest companies of each industry; then the stocks SEC does not classify, by issuer country; then
/// bonds by issuer type; then cash and other. Holdings are matched to SEC filers by name only, and no security
/// identifier is stored (ADR 0010). Compact for the client: each office, industry and company is listed once and
/// referred to by its index.
/// </summary>
public sealed class FundIndustriesSnapshotDto
{
    /// <summary>The latest report date of any fund ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>
    /// What the dataset was built with (the matching rules and the override list), so a refresh rebuilds it when either
    /// changes even if no fund has filed a newer report. Not shown on the page.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>The SEC industry offices, in SEC office-number order, with offices SEC does not number last.</summary>
    public List<string> Offices { get; set; } = [];

    /// <summary>The SIC industries, each within one office, in office order and then SIC code order.</summary>
    public List<SectorsIndustryDto> Industries { get; set; } = [];

    /// <summary>Every company listed for any fund, in name order.</summary>
    public List<FundIndustryCompanyDto> Companies { get; set; } = [];

    /// <summary>Each fund with a readable latest report, in the configured order.</summary>
    public List<FundIndustriesDto> Funds { get; set; } = [];
}

/// <summary>
/// A company in the fund industries dataset: an SEC filer some fund holds.
/// </summary>
public sealed class FundIndustryCompanyDto
{
    /// <summary>The company's name for display: SEC's name, title-cased when SEC has it in capitals.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The company's first ticker, or null when it has none (e.g. delisted since the report).</summary>
    public string? Ticker { get; set; }

    /// <summary>The company's industry: its index in <see cref="FundIndustriesSnapshotDto.Industries"/>.</summary>
    public int Industry { get; set; }
}

/// <summary>
/// One fund's holdings from its latest report, as shares of its net assets in percent. The industry shares sum every
/// holding placed in the industry, so they stay exact although only each industry's largest companies are listed.
/// </summary>
public sealed class FundIndustriesDto
{
    /// <summary>The fund's ticker.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The date the report is as of.</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>The report's EDGAR accession number, so a refresh knows when a fund has filed a newer one.</summary>
    public string Accession { get; set; } = string.Empty;

    /// <summary>Each industry the fund holds stocks in, largest first.</summary>
    public List<FundIndustryShareDto> Industries { get; set; } = [];

    /// <summary>The largest companies of each industry (up to ten), largest first within each industry.</summary>
    public List<FundCompanyShareDto> Companies { get; set; } = [];

    /// <summary>The stocks SEC does not classify (no SEC filer, or a filer with no SIC code), by issuer country.</summary>
    public List<FundUnclassifiedCountryDto> Unclassified { get; set; } = [];

    /// <summary>Bonds and other debt, by issuer type, largest first.</summary>
    public List<FundNamedShareDto> Bonds { get; set; } = [];

    /// <summary>
    /// The rest of the fund in percent: money-market and other short-term investments, derivatives, funds, cash not
    /// reported as a holding, and anything else.
    /// </summary>
    public decimal CashAndOther { get; set; }
}

/// <summary>
/// A fund's share in one SIC industry.
/// </summary>
public sealed class FundIndustryShareDto
{
    /// <summary>The industry's index in <see cref="FundIndustriesSnapshotDto.Industries"/>.</summary>
    public int Industry { get; set; }

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }

    /// <summary>How many companies the fund holds in the industry.</summary>
    public int CompanyCount { get; set; }
}

/// <summary>
/// A fund's share in one company.
/// </summary>
public sealed class FundCompanyShareDto
{
    /// <summary>The company's index in <see cref="FundIndustriesSnapshotDto.Companies"/>.</summary>
    public int Company { get; set; }

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }
}

/// <summary>
/// A fund's stocks of one country that SEC does not classify.
/// </summary>
public sealed class FundUnclassifiedCountryDto
{
    /// <summary>The ISO alpha-2 code of the country where the issuers are organized, or null when the fund gives none.</summary>
    public string? Country { get; set; }

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }

    /// <summary>How many issuers the fund holds from the country.</summary>
    public int IssuerCount { get; set; }

    /// <summary>The largest issuers (up to ten), largest first.</summary>
    public List<FundNamedShareDto> Issuers { get; set; } = [];
}

/// <summary>
/// A share of a fund with a name: an issuer, or a bond issuer type.
/// </summary>
public sealed class FundNamedShareDto
{
    /// <summary>The name for display.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The share of the fund's net assets, in percent.</summary>
    public decimal Percent { get; set; }
}
