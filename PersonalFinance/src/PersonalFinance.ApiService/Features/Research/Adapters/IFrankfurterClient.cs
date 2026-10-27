namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// Frankfurter v2's blended rate (localization spec §2.6): key-free, free for commercial use, one rate per calendar
/// day. The primary source of <see cref="FxRatesRefresher"/>.
/// </summary>
internal interface IFrankfurterClient
{
    /// <summary>
    /// The USD to <paramref name="quoteCurrency"/> rate for each day from <paramref name="from"/> to
    /// <paramref name="to"/>. Throws <see cref="HttpRequestException"/> when Frankfurter cannot be reached and
    /// <see cref="InvalidDataException"/> when its answer cannot be read.
    /// </summary>
    Task<IReadOnlyList<DailyRate>> GetUsdRatesAsync(string quoteCurrency, DateOnly from, DateOnly to,
        CancellationToken cancellationToken);
}
