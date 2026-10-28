namespace PersonalFinance.Shared.DTOs.CurrencyConverter;

/// <summary>
/// The result of one Currency Converter calculation. The rate is the cross rate through USD, unrounded; the converted
/// amount is rounded once to <see cref="To"/>'s minor units. With no rate for a leg the conversion is
/// <see cref="Unavailable"/> and carries no amount and no rate, never zero.
/// </summary>
public sealed class CurrencyConversionDto
{
    /// <summary>The amount converted, in <see cref="From"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>The ISO 4217 code the amount is in.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The ISO 4217 code the amount was converted into.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>The day asked for; <c>null</c> for the latest rate.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>The amount in <see cref="To"/>, rounded to its minor units; <c>null</c> when unavailable.</summary>
    public decimal? ConvertedAmount { get; set; }

    /// <summary>Units of <see cref="To"/> per one <see cref="From"/>, unrounded; <c>null</c> when unavailable.</summary>
    public decimal? Rate { get; set; }

    /// <summary>The day of the rate used: the older of the two legs. <c>null</c> when no rate was needed or found.</summary>
    public DateOnly? RateDate { get; set; }

    /// <summary>Where the rates came from (<c>frankfurter</c>, <c>currency-api</c>), one entry per distinct source.</summary>
    public List<string> Sources { get; set; } = [];

    /// <summary>True when <see cref="RateDate"/> is more than three days before the day asked for (or today).</summary>
    public bool IsStale { get; set; }

    /// <summary>True when a leg had no stored rate at all.</summary>
    public bool Unavailable { get; set; }
}
