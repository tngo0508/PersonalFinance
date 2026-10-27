namespace PersonalFinance.Data.Entities;

/// <summary>
/// One day's exchange rate from <see cref="BaseCurrency"/> to <see cref="QuoteCurrency"/> (ADR 0016), written by the
/// daily rate refresh. It is system data like <see cref="ResearchSnapshot"/>, not user-owned, so it is not an
/// <see cref="AuditableEntity"/> and is never soft-deleted. USD is the only base; the inverse rate is computed.
/// </summary>
public class ExchangeRate
{
    public int Id { get; set; }

    /// <summary>The ISO 4217 code one unit of which <see cref="Rate"/> prices: always <c>USD</c>.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>The ISO 4217 code the rate is quoted in, e.g. <c>VND</c>.</summary>
    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>The day the rate is for; one row per base, quote and day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Units of <see cref="QuoteCurrency"/> per one <see cref="BaseCurrency"/>, e.g. 25818 for USD to VND.</summary>
    public decimal Rate { get; set; }

    /// <summary>Where the rate came from: <c>frankfurter</c> (primary) or <c>currency-api</c> (fallback).</summary>
    public string Source { get; set; } = string.Empty;
}
