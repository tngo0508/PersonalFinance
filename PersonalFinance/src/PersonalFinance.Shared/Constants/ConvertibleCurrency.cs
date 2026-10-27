namespace PersonalFinance.Shared.Constants;

/// <summary>A convertible currency: what the converter needs to name it and round an amount in it.</summary>
public sealed record ConvertibleCurrency
{
    /// <summary>ISO 4217 code, three upper-case letters (<c>JPY</c>).</summary>
    public required string Code { get; init; }

    /// <summary>ISO 4217 minor units: the decimals shown and rounded to (2 for EUR, 0 for JPY).</summary>
    public required int MinorUnits { get; init; }

    /// <summary>The <c>en.json</c> key of the currency's display name (<c>Common.Currency.Name.JPY</c>).</summary>
    public required string NameKey { get; init; }

    /// <summary><paramref name="amount"/> rounded to this currency's minor units, halves away from zero.</summary>
    public decimal Round(decimal amount) => decimal.Round(amount, MinorUnits, MidpointRounding.AwayFromZero);

    /// <summary><paramref name="amount"/> rounded as <see cref="Round(decimal)"/> does; null stays null.</summary>
    public decimal? Round(decimal? amount) => amount is { } value ? Round(value) : null;
}
