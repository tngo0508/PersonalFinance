namespace PersonalFinance.Shared.Constants;

/// <summary>A supported currency: what the money formatter needs to render an amount in it.</summary>
public sealed record SupportedCurrency
{
    /// <summary>ISO 4217 code, three upper-case letters (<c>VND</c>).</summary>
    public required string Code { get; init; }

    /// <summary>English display name (<c>Vietnamese đồng</c>).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Decimals shown and rounded to: 2 for USD, 0 for VND.</summary>
    public required int MinorUnits { get; init; }

    /// <summary>The symbol shown with the amount in every culture (<c>$</c>, <c>₫</c>).</summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// <paramref name="amount"/> rounded to this currency's minor units, halves away from zero (localization spec §2.5):
    /// how an amount is stored, so a VND amount is saved as whole đồng.
    /// </summary>
    public decimal Round(decimal amount) => decimal.Round(amount, MinorUnits, MidpointRounding.AwayFromZero);

    /// <summary><paramref name="amount"/> rounded as <see cref="Round(decimal)"/> does; null stays null.</summary>
    public decimal? Round(decimal? amount) => amount is { } value ? Round(value) : null;
}
