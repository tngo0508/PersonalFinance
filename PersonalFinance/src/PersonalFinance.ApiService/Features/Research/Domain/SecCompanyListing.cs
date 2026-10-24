namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One row of SEC's company ticker list (<c>company_tickers_exchange.json</c>): a ticker and the company it belongs
/// to. A company with several share classes has one row per ticker.
/// </summary>
/// <param name="Cik">The company's SEC Central Index Key.</param>
/// <param name="Ticker">The ticker as SEC lists it, e.g. <c>BRK-B</c>.</param>
/// <param name="Name">The company's name as SEC lists it.</param>
/// <param name="Exchange">The exchange, e.g. <c>Nasdaq</c>, or null when SEC lists none.</param>
internal sealed record SecCompanyListing(long Cik, string Ticker, string Name, string? Exchange);
