namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// How a concept's value is dated, which decides the fiscal year it belongs to.
/// </summary>
internal enum CompanyFactTiming
{
    /// <summary>A full fiscal year's flow, such as revenue: a duration of about a year.</summary>
    Year,

    /// <summary>A balance at the fiscal year end, such as total assets.</summary>
    YearEnd,

    /// <summary>A cover-page figure dated near the filing, such as shares outstanding: the annual report's own year.</summary>
    Cover,
}
