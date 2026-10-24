namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The Research page's World dataset: per year, each country's stock-market capitalisation (World Bank WDI
/// <c>CM.MKT.LCAP.CD</c>) and that market cap as % of GDP (<c>NY.GDP.MKTP.CD</c>). Compact for the client: each
/// country is listed once in <see cref="Countries"/> and referred to by its index. A value the World Bank does not have
/// is absent, never zero.
/// </summary>
public sealed class WorldSnapshotDto : IResearchSnapshotDto<WorldPeriodDto>
{
    /// <summary>The date the World Bank last updated the market-cap series ("Data as of" on the page).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The years with any market cap, oldest first, e.g. <c>1975</c> to <c>2025</c>; <see cref="Values"/> follows this order.</summary>
    public List<string> Periods { get; set; } = [];

    /// <summary>Every country with a market cap in any year, in ISO numeric order.</summary>
    public List<WorldCountryDto> Countries { get; set; } = [];

    /// <summary>One entry per year, in the order of <see cref="Periods"/>.</summary>
    public List<WorldPeriodDto> Values { get; set; } = [];
}

/// <summary>
/// A country of the World dataset.
/// </summary>
public sealed class WorldCountryDto
{
    /// <summary>
    /// The ISO 3166-1 numeric code as three digits, e.g. <c>840</c> or <c>076</c>: the id of the country's shape in
    /// the world-atlas map.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The ISO 3166-1 alpha-3 code the World Bank uses, e.g. <c>USA</c>.</summary>
    public string Iso3 { get; set; } = string.Empty;

    /// <summary>The World Bank's name for the country, e.g. <c>United States</c>.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// One year of the World dataset: the countries with a market cap that year.
/// </summary>
public sealed class WorldPeriodDto
{
    /// <summary>The year, e.g. <c>2024</c>.</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The countries with a market cap that year, largest first. A country without one is not listed.</summary>
    public List<WorldCountryValueDto> Countries { get; set; } = [];
}

/// <summary>
/// One country's figures in one year.
/// </summary>
public sealed class WorldCountryValueDto
{
    /// <summary>The country's index in <see cref="WorldSnapshotDto.Countries"/>.</summary>
    public int Country { get; set; }

    /// <summary>The market capitalisation of listed domestic companies (current US$, to the dollar).</summary>
    public decimal MarketCap { get; set; }

    /// <summary>The market cap as % of GDP, to one decimal place; null when the World Bank has no GDP for that year.</summary>
    public decimal? PercentOfGdp { get; set; }
}
