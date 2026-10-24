namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's Sectors dataset: per year, the largest SEC filers by Public Float, grouped as SEC industry
/// office, then SIC industry, then company, with the rest summed into "Other" per industry. Compact for the client:
/// each office, industry and company is listed once and referred to by its index in <see cref="Offices"/>,
/// <see cref="Industries"/> or <see cref="Companies"/>.
/// </summary>
public sealed class SectorsSnapshotDto
{
    /// <summary>The date the source data is as of ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The period labels, oldest first, e.g. <c>2011</c> to <c>2025</c>; <see cref="Values"/> follows this order.</summary>
    public List<string> Periods { get; set; } = [];

    /// <summary>
    /// The SEC industry offices, e.g. <c>Technology</c>, in SEC office-number order, with <c>Unassigned</c> last.
    /// </summary>
    public List<string> Offices { get; set; } = [];

    /// <summary>The SIC industries, each within one office, in office order and then SIC code order.</summary>
    public List<SectorsIndustryDto> Industries { get; set; } = [];

    /// <summary>Every company ranked in any period, in CIK order.</summary>
    public List<SectorsCompanyDto> Companies { get; set; } = [];

    /// <summary>One entry per period, in the order of <see cref="Periods"/>.</summary>
    public List<SectorsPeriodDto> Values { get; set; } = [];
}

/// <summary>
/// A SIC industry within one SEC industry office.
/// </summary>
public sealed class SectorsIndustryDto
{
    /// <summary>The SIC code, e.g. <c>3674</c>, or null for filers SEC has not classified.</summary>
    public string? SicCode { get; set; }

    /// <summary>The SIC description for display, e.g. <c>Semiconductors &amp; Related Devices</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The industry's office: its index in <see cref="SectorsSnapshotDto.Offices"/>.</summary>
    public int Office { get; set; }
}

/// <summary>
/// A company in the Sectors dataset.
/// </summary>
public sealed class SectorsCompanyDto
{
    /// <summary>The SEC Central Index Key of the filer.</summary>
    public long Cik { get; set; }

    /// <summary>The filer's name for display: SEC's name, title-cased when SEC has it in capitals.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The filer's first ticker, or null when it has none.</summary>
    public string? Ticker { get; set; }

    /// <summary>The filer's industry: its index in <see cref="SectorsSnapshotDto.Industries"/>.</summary>
    public int Industry { get; set; }
}

/// <summary>
/// One period of the Sectors dataset: the ranked largest filers, and the rest summed per industry and in total.
/// </summary>
public sealed class SectorsPeriodDto
{
    /// <summary>The period label, e.g. <c>2025</c>.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The largest filers by Public Float, largest first.</summary>
    public List<SectorsRankedValueDto> Ranked { get; set; } = [];

    /// <summary>
    /// The filers not in <see cref="Ranked"/>, summed per industry (the sunburst's "Other" slices), in industry order.
    /// </summary>
    public List<SectorsOtherValueDto> Other { get; set; } = [];

    /// <summary>The summed Public Float (US$) of every filer not in <see cref="Ranked"/>.</summary>
    public decimal RestTotal { get; set; }

    /// <summary>How many filers <see cref="RestTotal"/> sums.</summary>
    public int RestCount { get; set; }
}

/// <summary>
/// A ranked filer's Public Float in one period.
/// </summary>
public sealed class SectorsRankedValueDto
{
    /// <summary>The filer's index in <see cref="SectorsSnapshotDto.Companies"/>.</summary>
    public int Company { get; set; }

    /// <summary>The filer's Public Float (US$): the market value of its shares held by non-affiliates.</summary>
    public decimal PublicFloat { get; set; }
}

/// <summary>
/// The filers of one industry outside the ranked largest, summed for one period.
/// </summary>
public sealed class SectorsOtherValueDto
{
    /// <summary>The industry's index in <see cref="SectorsSnapshotDto.Industries"/>.</summary>
    public int Industry { get; set; }

    /// <summary>The summed Public Float (US$) of the industry's filers outside the ranked largest.</summary>
    public decimal PublicFloat { get; set; }

    /// <summary>How many filers <see cref="PublicFloat"/> sums.</summary>
    public int Count { get; set; }
}
