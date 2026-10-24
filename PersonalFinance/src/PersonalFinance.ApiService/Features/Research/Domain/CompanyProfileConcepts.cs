namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The US GAAP and cover-page concepts a Company Profile reads, in priority order per figure: for each fiscal year the
/// first concept with a value wins, so a company that moved from <c>SalesRevenueNet</c> to
/// <c>RevenueFromContractWithCustomerExcludingAssessedTax</c> still has one revenue series.
/// </summary>
internal static class CompanyProfileConcepts
{
    private const string UsGaap = "us-gaap";
    private const string Dei = "dei";
    private const string Usd = "USD";
    private const string UsdPerShare = "USD/shares";
    private const string Shares = "shares";

    /// <summary>Every concept read, grouped by figure in priority order.</summary>
    public static readonly IReadOnlyList<CompanyConcept> All =
    [
        new(CompanyFigure.Revenue, UsGaap, "Revenues", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.Revenue, UsGaap, "RevenueFromContractWithCustomerExcludingAssessedTax", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.Revenue, UsGaap, "SalesRevenueNet", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.GrossProfit, UsGaap, "GrossProfit", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.NetIncome, UsGaap, "NetIncomeLoss", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.DilutedEps, UsGaap, "EarningsPerShareDiluted", UsdPerShare, CompanyFactTiming.Year),
        new(CompanyFigure.OperatingCashFlow, UsGaap, "NetCashProvidedByUsedInOperatingActivities", Usd, CompanyFactTiming.Year),
        new(CompanyFigure.TotalAssets, UsGaap, "Assets", Usd, CompanyFactTiming.YearEnd),
        new(CompanyFigure.TotalLiabilities, UsGaap, "Liabilities", Usd, CompanyFactTiming.YearEnd),
        new(CompanyFigure.LiabilitiesAndEquity, UsGaap, "LiabilitiesAndStockholdersEquity", Usd, CompanyFactTiming.YearEnd),
        new(CompanyFigure.Equity, UsGaap, "StockholdersEquity", Usd, CompanyFactTiming.YearEnd),
        new(CompanyFigure.Equity, UsGaap, "StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest", Usd, CompanyFactTiming.YearEnd),
        new(CompanyFigure.SharesOutstanding, Dei, "EntityCommonStockSharesOutstanding", Shares, CompanyFactTiming.Cover),
        new(CompanyFigure.PublicFloat, Dei, "EntityPublicFloat", Usd, CompanyFactTiming.Cover),
    ];

    /// <summary>
    /// The annual reports figures are read from: 10-K for US companies; 20-F and 40-F for foreign companies, whose
    /// figures are read only when they report under US GAAP in US dollars.
    /// </summary>
    public static readonly IReadOnlySet<string> AnnualForms =
        new HashSet<string>(StringComparer.Ordinal) { "10-K", "10-K/A", "20-F", "20-F/A", "40-F", "40-F/A" };
}
