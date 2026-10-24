namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads interest rates from the Federal Reserve Board's H.15 release ("Selected Interest Rates"), public domain with
/// the Board credited as the source. The release's whole data set is one zip of SDMX XML (about 4 MB, 70 MB
/// unpacked); it is read as a stream and only the wanted series are kept. FRED is not used: its terms forbid caching.
/// </summary>
internal interface IFedRatesClient
{
    /// <summary>
    /// The 3-month Treasury bill rate (secondary market, discount basis), monthly average, in percent a year, keyed
    /// by the first day of its month, oldest first.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(CancellationToken cancellationToken);

    /// <summary>The effective federal funds rate and the 3-month Treasury bill rate, monthly, from one download.</summary>
    Task<FedMonthlyRates> GetMonthlyRatesAsync(CancellationToken cancellationToken);
}

/// <summary>Monthly averages from H.15, in percent a year, each keyed by the first day of its month, oldest first.</summary>
/// <param name="FedFunds">The effective federal funds rate.</param>
/// <param name="TreasuryBill">The 3-month Treasury bill rate (secondary market, discount basis).</param>
internal sealed record FedMonthlyRates(IReadOnlyList<(DateOnly Month, decimal Rate)> FedFunds,
    IReadOnlyList<(DateOnly Month, decimal Rate)> TreasuryBill);
