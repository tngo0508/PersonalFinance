namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// The fawazahmed0 currency-api (CC0) on jsDelivr, with its Cloudflare Pages mirror: key-free, latest rate only. The
/// fallback of <see cref="FxRatesRefresher"/>; its sources are undisclosed, so it is never the primary.
/// </summary>
internal interface ICurrencyApiClient
{
    /// <summary>
    /// The latest USD to <paramref name="quoteCurrency"/> rate, from jsDelivr or, when that fails, the mirror. Throws
    /// <see cref="HttpRequestException"/> or <see cref="InvalidDataException"/> when neither answers a rate.
    /// </summary>
    Task<DailyRate> GetLatestUsdRateAsync(string quoteCurrency, CancellationToken cancellationToken);
}
