namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// A figure of a Company Profile's fiscal year, read from one or more XBRL concepts.
/// </summary>
internal enum CompanyFigure
{
    Revenue,
    GrossProfit,
    NetIncome,
    DilutedEps,
    OperatingCashFlow,
    TotalAssets,
    TotalLiabilities,
    LiabilitiesAndEquity,
    Equity,
    SharesOutstanding,
    PublicFloat,
}
