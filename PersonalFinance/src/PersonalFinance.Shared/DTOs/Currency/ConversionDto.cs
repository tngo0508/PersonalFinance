namespace PersonalFinance.Shared.DTOs.Currency;

/// <summary>
/// How a result's amounts were brought into the Account's Reporting Currency (ADR 0016, localization spec §2.6).
/// Totals in the result are already in <see cref="ReportingCurrency"/>; this says which rates did it, so the page
/// can caption them "converted at {rate} on {date}", "rate as of {date}" when a rate is more than three days older than
/// the day it stood in for, or "conversion unavailable" when there was no rate at all (the amounts then show in their
/// original currency, never as zero).
/// </summary>
public sealed class ConversionDto
{
    /// <summary>The ISO 4217 code every total is in.</summary>
    public string ReportingCurrency { get; set; } = string.Empty;

    /// <summary>True when at least one amount was in another currency, so a caption is due.</summary>
    public bool Converted { get; set; }

    /// <summary>True when at least one amount had no rate at all and is shown in its original currency.</summary>
    public bool Unavailable { get; set; }

    /// <summary>The rates used, one per currency pair (USD is the only base).</summary>
    public List<ConversionRateDto> Rates { get; set; } = [];

    /// <summary>
    /// One caption for several results: converted or unavailable when any part is, and per pair the most recent rate
    /// used, stale or dated when any part's was. Null when there is nothing to combine.
    /// </summary>
    public static ConversionDto? Combine(IEnumerable<ConversionDto?> parts)
    {
        List<ConversionDto> present = parts.Where(p => p is not null).Cast<ConversionDto>().ToList();
        if (present.Count == 0)
        {
            return null;
        }

        return new ConversionDto
        {
            ReportingCurrency = present[0].ReportingCurrency,
            Converted = present.Any(p => p.Converted),
            Unavailable = present.Any(p => p.Unavailable),
            Rates = present.SelectMany(p => p.Rates)
                .GroupBy(r => (r.BaseCurrency, r.QuoteCurrency))
                .Select(pair =>
                {
                    ConversionRateDto latest = pair.OrderByDescending(r => r.Date).First();
                    return new ConversionRateDto
                    {
                        BaseCurrency = latest.BaseCurrency,
                        QuoteCurrency = latest.QuoteCurrency,
                        Rate = latest.Rate,
                        Date = latest.Date,
                        IsStale = pair.Any(r => r.IsStale),
                        IsDated = pair.Any(r => r.IsDated)
                    };
                })
                .OrderBy(r => r.QuoteCurrency, StringComparer.Ordinal)
                .ToList()
        };
    }
}

/// <summary>One exchange rate a conversion used: units of <see cref="QuoteCurrency"/> per one <see cref="BaseCurrency"/>.</summary>
public sealed class ConversionRateDto
{
    /// <summary>The base of the stored rate, always <c>USD</c>.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>The quoted currency, e.g. <c>VND</c>.</summary>
    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>Units of the quote currency per one unit of the base.</summary>
    public decimal Rate { get; set; }

    /// <summary>The day the rate is for: the most recent one used when amounts were dated.</summary>
    public DateOnly Date { get; set; }

    /// <summary>True when a rate stood in for a day more than three days after it (the nearest prior rate was used).</summary>
    public bool IsStale { get; set; }

    /// <summary>True when dated amounts (transactions, monthly lines) each used their own day's rate.</summary>
    public bool IsDated { get; set; }
}
