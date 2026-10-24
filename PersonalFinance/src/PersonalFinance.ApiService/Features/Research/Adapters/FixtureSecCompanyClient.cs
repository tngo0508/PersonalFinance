using System.Globalization;
using PersonalFinance.ApiService.Features.Research.Domain;

namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// A made-up SEC for the E2E walkthrough, used instead of <see cref="SecEdgarClient"/> when
/// <see cref="ResearchFixtureSeeder.IsEnabled"/>: CI and local E2E runs cannot reach SEC. It lists three tickers:
/// <list type="bullet">
/// <item><c>EXMP</c>, Example Corp: four fiscal years (2022 to 2025) of US GAAP figures, with no gross profit in 2022,
/// a change of revenue concept in 2023, a restated 2024 net income, and 10-K, 10-Q and 8-K filings.</item>
/// <item><c>EXFD</c>, Example Index Fund: a fund share class.</item>
/// <item><c>EXGL</c>, Example Global plc: a foreign company reporting under IFRS on Form 20-F.</item>
/// </list>
/// The figures are rounded illustrations, not source data, and are never used outside Development.
/// </summary>
internal sealed class FixtureSecCompanyClient : ISecCompanyClient
{
    /// <summary>Example Corp's CIK; made up, outside SEC's range of real filers.</summary>
    public const long ExampleCorpCik = 9_000_001;

    /// <summary>Example Global plc's CIK; made up.</summary>
    public const long ExampleGlobalCik = 9_000_003;

    private const long ExampleFundCik = 9_000_002;

    /// <inheritdoc />
    public Task<IReadOnlyList<SecCompanyListing>> GetCompanyTickersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SecCompanyListing>>(
        [
            new SecCompanyListing(ExampleCorpCik, "EXMP", "EXAMPLE CORP", "NYSE"),
            new SecCompanyListing(ExampleFundCik, "EXFD", "EXAMPLE INDEX FUND", "NYSE"),
            new SecCompanyListing(ExampleGlobalCik, "EXGL", "Example Global plc", "NYSE"),
        ]);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetFundTickersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["EXFD"]);

    /// <inheritdoc />
    public Task<SecCompanySubmissions?> GetCompanySubmissionsAsync(long cik, CancellationToken cancellationToken) =>
        Task.FromResult(cik switch
        {
            ExampleCorpCik => new SecCompanySubmissions(ExampleCorpCik, "EXAMPLE CORP", ["EXMP"], ["NYSE"], "7372",
                "SERVICES-PREPACKAGED SOFTWARE", "06 Technology", "DE", "1231",
            [
                Filing("0009000001-26-000012", "8-K", "2026-08-04", "2026-08-04", "2.02,9.01"),
                Filing("0009000001-26-000011", "10-Q", "2026-08-04", "2026-06-30"),
                Filing("0009000001-26-000010", "8-K", "2026-06-12", "2026-06-10", "5.02"),
                Filing("0009000001-26-000009", "4", "2026-05-20", null),
                Filing("0009000001-26-000008", "10-Q", "2026-05-05", "2026-03-31"),
                Filing("0009000001-26-000007", "8-K", "2026-05-05", "2026-05-05", "2.02,7.01,9.01"),
                Filing("0009000001-26-000004", "10-K", "2026-02-20", "2025-12-31"),
                Filing("0009000001-25-000004", "10-K", "2025-02-21", "2024-12-31"),
            ]),
            ExampleGlobalCik => new SecCompanySubmissions(ExampleGlobalCik, "Example Global plc", ["EXGL"], ["NYSE"],
                "2834", "PHARMACEUTICAL PREPARATIONS", "03 Life Sciences", "United Kingdom", "1231",
            [
                Filing("0009000003-26-000003", "6-K", "2026-07-30", "2026-06-30"),
                Filing("0009000003-26-000001", "20-F", "2026-03-10", "2025-12-31"),
            ]),
            _ => null,
        });

    /// <inheritdoc />
    public Task<SecCompanyFacts?> GetCompanyFactsAsync(long cik, CancellationToken cancellationToken) =>
        Task.FromResult(cik switch
        {
            ExampleCorpCik => ExampleCorpFacts(),
            ExampleGlobalCik => new SecCompanyFacts(new Dictionary<string, IReadOnlyList<SecFact>>(), HasIfrsFacts: true),
            _ => null,
        });

    private static SecFilingEntry Filing(string accession, string form, string filed, string? reported,
        string items = "") =>
        new(accession, form, Date(filed), reported is null ? null : Date(reported),
            items.Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Example Corp's facts, as its 10-Ks for fiscal 2023 to 2025 report them (each with the prior year).</summary>
    private static SecCompanyFacts ExampleCorpFacts()
    {
        // (fiscal year, revenue, gross profit, net income, diluted EPS, operating cash flow, assets, liabilities,
        // equity, shares outstanding, public float), in US dollars.
        (int Year, decimal Revenue, decimal? Gross, decimal Net, decimal Eps, decimal Cash, decimal Assets,
            decimal Liabilities, decimal Equity, decimal Shares, decimal Float)[] years =
        [
            (2022, 1_200_000_000m, null, 90_000_000m, 0.90m, 150_000_000m, 2_000_000_000m, 1_100_000_000m, 900_000_000m, 100_000_000m, 3_000_000_000m),
            (2023, 1_380_000_000m, 800_000_000m, 120_000_000m, 1.20m, 190_000_000m, 2_300_000_000m, 1_250_000_000m, 1_050_000_000m, 100_000_000m, 3_600_000_000m),
            (2024, 1_520_000_000m, 900_000_000m, -40_000_000m, -0.40m, 120_000_000m, 2_400_000_000m, 1_420_000_000m, 980_000_000m, 101_000_000m, 2_900_000_000m),
            (2025, 1_710_000_000m, 1_020_000_000m, 160_000_000m, 1.58m, 240_000_000m, 2_650_000_000m, 1_500_000_000m, 1_150_000_000m, 101_500_000m, 4_200_000_000m),
        ];

        var facts = new Dictionary<string, List<SecFact>>(StringComparer.Ordinal);
        void Add(string key, SecFact fact)
        {
            if (!facts.TryGetValue(key, out List<SecFact>? list))
            {
                facts[key] = list = [];
            }

            list.Add(fact);
        }

        // Each 10-K reports its own year and the prior year; the cover page only its own year.
        foreach ((int Year, decimal Revenue, decimal? Gross, decimal Net, decimal Eps, decimal Cash, decimal Assets,
                     decimal Liabilities, decimal Equity, decimal Shares, decimal Float) row in years.Skip(1))
        {
            int filingYear = row.Year;
            string accession = string.Create(CultureInfo.InvariantCulture, $"0009000001-{(filingYear + 1) % 100:00}-000004");
            DateOnly filed = new(filingYear + 1, 2, 20);
            foreach ((int Year, decimal Revenue, decimal? Gross, decimal Net, decimal Eps, decimal Cash, decimal Assets,
                         decimal Liabilities, decimal Equity, decimal Shares, decimal Float) reported in
                     years.Where(y => y.Year == filingYear || y.Year == filingYear - 1))
            {
                DateOnly start = new(reported.Year, 1, 1);
                DateOnly end = new(reported.Year, 12, 31);
                SecFact Flow(decimal value) => new(start, end, value, filingYear, "FY", "10-K", filed, accession);
                SecFact Balance(decimal value) => new(null, end, value, filingYear, "FY", "10-K", filed, accession);

                // Revenue moved from SalesRevenueNet to the ASC 606 concept from fiscal 2023.
                Add(reported.Year < 2023 ? "us-gaap:SalesRevenueNet" : "us-gaap:RevenueFromContractWithCustomerExcludingAssessedTax",
                    Flow(reported.Revenue));
                if (reported.Gross is { } gross)
                {
                    Add("us-gaap:GrossProfit", Flow(gross));
                }

                // The fiscal 2025 10-K restates fiscal 2024's net loss from -$40M to -$35M.
                decimal net = reported.Year == 2024 && filingYear == 2025 ? -35_000_000m : reported.Net;
                Add("us-gaap:NetIncomeLoss", Flow(net));
                Add("us-gaap:EarningsPerShareDiluted", Flow(reported.Eps));
                Add("us-gaap:NetCashProvidedByUsedInOperatingActivities", Flow(reported.Cash));
                Add("us-gaap:Assets", Balance(reported.Assets));
                Add("us-gaap:Liabilities", Balance(reported.Liabilities));
                Add("us-gaap:StockholdersEquity", Balance(reported.Equity));
            }

            Add("dei:EntityCommonStockSharesOutstanding",
                new SecFact(null, new DateOnly(filingYear + 1, 2, 10), row.Shares, filingYear, "FY", "10-K", filed, accession));
            Add("dei:EntityPublicFloat",
                new SecFact(null, new DateOnly(filingYear, 6, 30), row.Float, filingYear, "FY", "10-K", filed, accession));
        }

        return new SecCompanyFacts(
            facts.ToDictionary(p => p.Key, p => (IReadOnlyList<SecFact>)p.Value, StringComparer.Ordinal),
            HasIfrsFacts: false);
    }

    private static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
