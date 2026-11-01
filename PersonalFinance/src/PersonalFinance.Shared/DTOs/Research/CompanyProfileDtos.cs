namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The kinds of answer a Company Profile lookup can give (<see cref="CompanyLookupResultDto.Kind"/>).
/// </summary>
public static class CompanyLookupKinds
{
    /// <summary>An SEC-registered company: <see cref="CompanyLookupResultDto.Profile"/> holds its Company Profile.</summary>
    public const string Profile = "profile";

    /// <summary>A mutual fund or ETF share class: funds are shown on the Funds page, not as a Company
    /// Profile.</summary>
    public const string Fund = "fund";

    /// <summary>A ticker SEC does not list.</summary>
    public const string NotFound = "notFound";
}

/// <summary>
/// One suggestion for the Research company lookup's search box, from SEC's company ticker list.
/// </summary>
public sealed class CompanySuggestionDto
{
    /// <summary>The ticker as SEC lists it, e.g. <c>MSFT</c> or <c>BRK-B</c>.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The company's name as SEC lists it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The exchange the ticker trades on, e.g. <c>Nasdaq</c>, or null when SEC lists none.</summary>
    public string? Exchange { get; set; }

    /// <summary>True when the ticker is a fund share class, which the Funds page covers instead.</summary>
    public bool IsFund { get; set; }
}

/// <summary>
/// The answer to a Company Profile lookup by ticker: a profile, a fund, or a ticker SEC does not list.
/// </summary>
public sealed class CompanyLookupResultDto
{
    /// <summary>One of <see cref="CompanyLookupKinds"/>.</summary>
    public string Kind { get; set; } = CompanyLookupKinds.NotFound;

    /// <summary>The ticker that was looked up, in upper case.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The Company Profile when <see cref="Kind"/> is <see cref="CompanyLookupKinds.Profile"/>.</summary>
    public CompanyProfileDto? Profile { get; set; }

    /// <summary>
    /// True when the profile's press releases can be read; false for a fund, an unlisted ticker, or when ApiService
    /// has press releases switched off, in which case the page leaves the section out.
    /// </summary>
    public bool PressReleasesAvailable { get; set; }
}

/// <summary>
/// A Company Profile (GLOSSARY.md): the identity, reported annual financial statements and recent SEC filings of one
/// SEC-registered company, for information only. Every figure is as the company reported it in US dollars under US
/// GAAP; one it did not report in that form is null, never zero or converted.
/// </summary>
public sealed class CompanyProfileDto
{
    /// <summary>The company's SEC Central Index Key.</summary>
    public long Cik { get; set; }

    /// <summary>The company's name as SEC reports it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The ticker the profile was looked up by.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The exchange of <see cref="Ticker"/>, or null when SEC reports none.</summary>
    public string? Exchange { get; set; }

    /// <summary>The four-digit SIC industry code, or null.</summary>
    public string? SicCode { get; set; }

    /// <summary>The SIC industry's description, e.g. <c>Services-Prepackaged Software</c>, or null.</summary>
    public string? SicDescription { get; set; }

    /// <summary>The SEC industry office that reviews the company's filings, e.g. <c>Technology</c>, or null.</summary>
    public string? IndustryOffice { get; set; }

    /// <summary>The state or country of incorporation as SEC describes it, e.g. <c>WA</c>, or null.</summary>
    public string? StateOfIncorporation { get; set; }

    /// <summary>The fiscal year end as a day and month, e.g. <c>30 June</c>, or null.</summary>
    public string? FiscalYearEnd { get; set; }

    /// <summary>
    /// True when the company reports figures under the IFRS taxonomy, which this profile does not read, so its
    /// financial figures may be missing.
    /// </summary>
    public bool ReportsUnderIfrs { get; set; }

    /// <summary>The filing date of the latest filing the profile is built from ("Data as of"), or null.</summary>
    public DateOnly? DataAsOf { get; set; }

    /// <summary>Up to ten fiscal years with any reported figure, oldest first.</summary>
    public List<CompanyFiscalYearDto> FiscalYears { get; set; } = [];

    /// <summary>The most recent periodic and current reports, newest first.</summary>
    public List<CompanyFilingDto> Filings { get; set; } = [];

    /// <summary>The company's page of every filing on EDGAR.</summary>
    public string AllFilingsUrl { get; set; } = string.Empty;
}

/// <summary>
/// One fiscal year of a Company Profile: the figures from the company's latest annual report for that year, and the
/// figures derived from them. Money is in US dollars, EPS in US dollars per share, shares in shares, and the derived
/// ratios are fractions (0.25 is 25%). A null is a figure that was not reported or cannot be derived.
/// </summary>
public sealed class CompanyFiscalYearDto
{
    /// <summary>The fiscal year as the company labels it, e.g. <c>2025</c>.</summary>
    public int FiscalYear { get; set; }

    /// <summary>The last day of the fiscal year, or null when only cover-page figures are known.</summary>
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>Revenue.</summary>
    public decimal? Revenue { get; set; }

    /// <summary>Gross profit.</summary>
    public decimal? GrossProfit { get; set; }

    /// <summary>Net income (loss) attributable to the company.</summary>
    public decimal? NetIncome { get; set; }

    /// <summary>Diluted earnings per share.</summary>
    public decimal? DilutedEps { get; set; }

    /// <summary>Net cash provided by (used in) operating activities.</summary>
    public decimal? OperatingCashFlow { get; set; }

    /// <summary>Total assets at the fiscal year end.</summary>
    public decimal? TotalAssets { get; set; }

    /// <summary>Total liabilities at the fiscal year end.</summary>
    public decimal? TotalLiabilities { get; set; }

    /// <summary>Shareholders' equity at the fiscal year end.</summary>
    public decimal? Equity { get; set; }

    /// <summary>Common shares outstanding, as on the cover of the annual report.</summary>
    public decimal? SharesOutstanding { get; set; }

    /// <summary>Public Float (GLOSSARY.md), as on the cover of the annual report.</summary>
    public decimal? PublicFloat { get; set; }

    /// <summary>Gross profit / revenue.</summary>
    public decimal? GrossMargin { get; set; }

    /// <summary>Net income / revenue.</summary>
    public decimal? NetMargin { get; set; }

    /// <summary>Revenue / prior year's revenue − 1.</summary>
    public decimal? RevenueGrowth { get; set; }

    /// <summary>Diluted EPS / prior year's diluted EPS − 1.</summary>
    public decimal? EpsGrowth { get; set; }

    /// <summary>Total liabilities / shareholders' equity.</summary>
    public decimal? LiabilitiesToEquity { get; set; }
}

/// <summary>
/// One SEC filing listed on a Company Profile.
/// </summary>
public sealed class CompanyFilingDto
{
    /// <summary>The form type, e.g. <c>10-K</c> or <c>8-K</c>.</summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>The date the filing was made.</summary>
    public DateOnly FiledOn { get; set; }

    /// <summary>The period the filing reports on, or null when it has none.</summary>
    public DateOnly? ReportedOn { get; set; }

    /// <summary>The filing's index page on EDGAR.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>For an 8-K, the events it reports in plain words, e.g. <c>Results of operations</c>; otherwise empty.</summary>
    public List<string> Events { get; set; } = [];
}
