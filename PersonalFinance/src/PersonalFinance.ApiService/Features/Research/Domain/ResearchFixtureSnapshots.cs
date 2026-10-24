using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Small, made-up Sectors, funds, fund overlap and World snapshots for the E2E walkthrough (stored by
/// <see cref="ResearchFixtureSeeder"/>): six companies over three years, SPY and QQQ over three quarters and six
/// countries over three years. The figures are rounded illustrations in the shape of the real snapshots, not source
/// data, and are never stored outside Development.
/// </summary>
internal static class ResearchFixtureSnapshots
{
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    /// <summary>The Sectors fixture: 2023 to 2025, with a Technology office and a chip maker under Manufacturing.</summary>
    public static SectorsSnapshotDto Sectors() => new()
    {
        AsOf = AsOf,
        Periods = ["2023", "2024", "2025"],
        Offices = ["Technology", "Manufacturing", "Finance", "Energy & Transportation"],
        Industries =
        [
            new() { SicCode = "3571", Name = "Electronic Computers", Office = 0, Technology = true },
            new() { SicCode = "7372", Name = "Services-Prepackaged Software", Office = 0, Technology = true },
            new() { SicCode = "3674", Name = "Semiconductors & Related Devices", Office = 1, Technology = true },
            new() { SicCode = "3711", Name = "Motor Vehicles & Passenger Car Bodies", Office = 1 },
            new() { SicCode = "6021", Name = "National Commercial Banks", Office = 2 },
            new() { SicCode = "2911", Name = "Petroleum Refining", Office = 3 },
        ],
        Companies =
        [
            new() { Cik = 19617, Name = "JPMorgan Chase & Co", Ticker = "JPM", Industry = 4 },
            new() { Cik = 34088, Name = "Exxon Mobil Corp", Ticker = "XOM", Industry = 5 },
            new() { Cik = 320193, Name = "Apple Inc.", Ticker = "AAPL", Industry = 0 },
            new() { Cik = 789019, Name = "Microsoft Corp", Ticker = "MSFT", Industry = 1 },
            new() { Cik = 1045810, Name = "Nvidia Corp", Ticker = "NVDA", Industry = 2 },
            new() { Cik = 1318605, Name = "Tesla, Inc.", Ticker = "TSLA", Industry = 3 },
        ],
        Values =
        [
            SectorsYear("2023", [(2, 2_050), (3, 1_780), (5, 560), (1, 440), (0, 410), (4, 280)]),
            SectorsYear("2024", [(2, 2_590), (3, 2_420), (4, 1_070), (5, 610), (1, 450), (0, 420)]),
            SectorsYear("2025", [(4, 2_890), (3, 2_840), (2, 2_620), (5, 1_020), (0, 530), (1, 480)]),
        ],
    };

    /// <summary>The funds fixture: SPY and QQQ for three quarters, SPY with common equity and cash vehicles.</summary>
    public static FundsSnapshotDto Funds() => new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Periods = ["2025 Q4", "2026 Q1", "2026 Q2"],
        Funds =
        [
            new() { Ticker = "SPY", Name = "SPDR S&P 500 ETF Trust", Role = "US broad" },
            new() { Ticker = "QQQ", Name = "Invesco QQQ Trust", Role = "Growth, sector and thematic" },
        ],
        Values =
        [
            FundsQuarter("2025 Q4", new DateOnly(2025, 12, 31), 0m),
            FundsQuarter("2026 Q1", new DateOnly(2026, 3, 31), 0.3m),
            FundsQuarter("2026 Q2", new DateOnly(2026, 6, 30), 0.6m),
        ],
    };

    /// <summary>
    /// The fund overlap fixture: SPY and QQQ from their 2026 Q2 reports, overlapping by 44.1% through six shared
    /// holdings.
    /// </summary>
    public static FundOverlapSnapshotDto FundOverlap() => new()
    {
        AsOf = new DateOnly(2026, 6, 30),
        Funds =
        [
            new() { Ticker = "SPY", ReportDate = new DateOnly(2026, 6, 30), Accession = "0000000000-26-000001" },
            new() { Ticker = "QQQ", ReportDate = new DateOnly(2026, 6, 30), Accession = "0000000000-26-000002" },
        ],
        Holdings = ["NVIDIA Corp", "Microsoft Corp", "Apple Inc", "Amazon.com Inc", "Broadcom Inc", "Meta Platforms Inc"],
        Pairs =
        [
            new()
            {
                A = 0, B = 1, Overlap = 44.1m, CoveredA = 46.2m, CoveredB = 87.5m, SharedCount = 101,
                Shared =
                [
                    new() { Holding = 0, WeightA = 7.7m, WeightB = 10.0m },
                    new() { Holding = 1, WeightA = 6.4m, WeightB = 8.6m },
                    new() { Holding = 2, WeightA = 6.0m, WeightB = 7.9m },
                    new() { Holding = 3, WeightA = 3.9m, WeightB = 5.4m },
                    new() { Holding = 4, WeightA = 2.5m, WeightB = 5.6m },
                    new() { Holding = 5, WeightA = 2.9m, WeightB = 3.5m },
                ],
            },
        ],
    };

    /// <summary>The World fixture: six countries for 2022 to 2024, one of them without a GDP figure in 2022.</summary>
    public static WorldSnapshotDto World() => new()
    {
        AsOf = new DateOnly(2026, 7, 1),
        Periods = ["2022", "2023", "2024"],
        Countries =
        [
            new() { Id = "076", Iso3 = "BRA", Name = "Brazil" },
            new() { Id = "156", Iso3 = "CHN", Name = "China" },
            new() { Id = "356", Iso3 = "IND", Name = "India" },
            new() { Id = "392", Iso3 = "JPN", Name = "Japan" },
            new() { Id = "826", Iso3 = "GBR", Name = "United Kingdom" },
            new() { Id = "840", Iso3 = "USA", Name = "United States" },
        ],
        Values =
        [
            WorldYear("2022", [(5, 40_300, 156.2m), (1, 10_600, 59.4m), (3, 5_380, 126.0m), (2, 3_390, 101.2m), (4, 2_850, null), (0, 690, 35.8m)]),
            WorldYear("2023", [(5, 46_200, 168.9m), (1, 9_880, 55.6m), (3, 6_130, 145.3m), (2, 4_350, 122.3m), (4, 2_880, 85.6m), (0, 900, 41.6m)]),
            WorldYear("2024", [(5, 54_100, 187.8m), (1, 11_200, 59.9m), (3, 6_300, 155.2m), (2, 5_140, 131.5m), (4, 2_960, 81.4m), (0, 730, 33.5m)]),
        ],
    };

    private const decimal Billion = 1_000_000_000m;

    /// <summary>One Sectors year: ranked (company, US$ billions) largest first, plus an "Other" slice per industry.</summary>
    private static SectorsPeriodDto SectorsYear(string period, (int Company, int Billions)[] ranked)
    {
        List<SectorsOtherValueDto> other =
        [
            new() { Industry = 1, PublicFloat = 1_150 * Billion, Count = 140 },
            new() { Industry = 2, PublicFloat = 980 * Billion, Count = 75 },
            new() { Industry = 4, PublicFloat = 1_420 * Billion, Count = 310 },
            new() { Industry = 5, PublicFloat = 760 * Billion, Count = 60 },
        ];
        return new SectorsPeriodDto
        {
            Period = period,
            Ranked = ranked.Select(r => new SectorsRankedValueDto { Company = r.Company, PublicFloat = r.Billions * Billion }).ToList(),
            Other = other,
            RestTotal = other.Sum(o => o.PublicFloat),
            RestCount = other.Sum(o => o.Count),
        };
    }

    /// <summary>One funds quarter; <paramref name="drift"/> moves the weights a little so quarters differ.</summary>
    private static FundsPeriodDto FundsQuarter(string period, DateOnly reportDate, decimal drift) => new()
    {
        Period = period,
        Funds =
        [
            new()
            {
                Ticker = "SPY", ReportDate = reportDate, NetAssets = (640 + (drift * 50)) * Billion, HoldingCount = 504,
                Categories =
                [
                    Category("EC", "Common equity", 502, 91.6m - drift,
                    [
                        ("NVIDIA Corp", 7.1m + drift), ("Microsoft Corp", 6.4m), ("Apple Inc", 6.0m),
                        ("Amazon.com Inc", 3.9m), ("Alphabet Inc", 2.0m),
                    ]),
                    Category("STIV", "Short-term investment vehicles", 2, 0m, [("State Street Navigator Securities Lending", 0.4m), ("State Street Institutional US Government Money Market Fund", 0.1m)]),
                ],
            },
            new()
            {
                Ticker = "QQQ", ReportDate = reportDate, NetAssets = (340 + (drift * 40)) * Billion, HoldingCount = 101,
                Categories =
                [
                    Category("EC", "Common equity", 101, 50.3m - drift,
                    [
                        ("NVIDIA Corp", 9.4m + drift), ("Microsoft Corp", 8.6m), ("Apple Inc", 7.9m),
                        ("Broadcom Inc", 5.6m), ("Amazon.com Inc", 5.4m), ("Meta Platforms Inc", 3.5m),
                    ]),
                ],
            },
        ],
    };

    /// <summary>A category whose weight is its listed holdings plus <paramref name="otherPercent"/>.</summary>
    private static FundCategoryDto Category(string code, string name, int holdingCount, decimal otherPercent,
        (string Name, decimal Percent)[] top) => new()
    {
        Code = code,
        Name = name,
        HoldingCount = holdingCount,
        Top = top.Select(t => new FundHoldingDto { Name = t.Name, Percent = t.Percent }).ToList(),
        OtherCount = holdingCount - top.Length,
        OtherPercent = otherPercent,
        Percent = top.Sum(t => t.Percent) + otherPercent,
    };

    /// <summary>One World year: (country, market cap in US$ billions, % of GDP) largest first.</summary>
    private static WorldPeriodDto WorldYear(string period, (int Country, int Billions, decimal? PercentOfGdp)[] countries) => new()
    {
        Period = period,
        Countries = countries
            .Select(c => new WorldCountryValueDto { Country = c.Country, MarketCap = c.Billions * Billion, PercentOfGdp = c.PercentOfGdp })
            .ToList(),
    };
}
