namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Reads the US government series the macro dataset needs besides the Fed's: consumer prices from the Bureau of Labor
/// Statistics, and Treasury bill rates from the Treasury (the fallback when the Fed's H.15 release cannot be read).
/// Both are public domain; BLS asks to be cited as the source.
/// </summary>
internal interface IUsAgencyClient
{
    /// <summary>
    /// CPI-U, all items, US city average, not seasonally adjusted (BLS series <c>CUUR0000SA0</c>, 1982-84 = 100),
    /// monthly from January of <paramref name="firstYear"/>, keyed by the first day of its month, oldest first.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Month, decimal Index)>> GetConsumerPricesAsync(int firstYear,
        CancellationToken cancellationToken);

    /// <summary>
    /// The 13-week Treasury bill rate (bank discount, close of day), averaged per month for <paramref name="year"/>,
    /// in percent a year, keyed by the first day of its month, oldest first.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Month, decimal Rate)>> GetTreasuryBillRatesAsync(int year,
        CancellationToken cancellationToken);
}
