namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's Sectors dataset: per year, the largest SEC filers by Public Float, with the rest summed.
/// Compact for the client: each company is listed once in <see cref="Companies"/> and referred to by its index there.
/// </summary>
public sealed class SectorsSnapshotDto
{
    /// <summary>The date the source data is as of ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The period labels, oldest first, e.g. <c>2011</c> to <c>2025</c>; <see cref="Values"/> follows this order.</summary>
    public List<string> Periods { get; set; } = [];

    /// <summary>Every company ranked in any period, in CIK order.</summary>
    public List<SectorsCompanyDto> Companies { get; set; } = [];

    /// <summary>One entry per period, in the order of <see cref="Periods"/>.</summary>
    public List<SectorsPeriodDto> Values { get; set; } = [];
}

/// <summary>
/// A company in the Sectors dataset.
/// </summary>
public sealed class SectorsCompanyDto
{
    /// <summary>The SEC Central Index Key of the filer.</summary>
    public long Cik { get; set; }

    /// <summary>The filer's name as SEC reports it.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// One period of the Sectors dataset: the ranked largest filers and the total of the rest.
/// </summary>
public sealed class SectorsPeriodDto
{
    /// <summary>The period label, e.g. <c>2025</c>.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The largest filers by Public Float, largest first.</summary>
    public List<SectorsRankedValueDto> Ranked { get; set; } = [];

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
