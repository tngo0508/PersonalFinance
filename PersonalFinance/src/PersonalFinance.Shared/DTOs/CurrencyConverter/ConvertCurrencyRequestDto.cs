namespace PersonalFinance.Shared.DTOs.CurrencyConverter;

/// <summary>
/// One Currency Converter calculation: <see cref="Amount"/> of <see cref="From"/> into <see cref="To"/>, at the rate
/// of <see cref="Date"/> or the latest. Every property is optional here so the validator, not model binding, names
/// what is missing.
/// </summary>
public sealed class ConvertCurrencyRequestDto
{
    /// <summary>The amount to convert, in <see cref="From"/>; zero or more.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The ISO 4217 code of a convertible currency the amount is in.</summary>
    public string? From { get; set; }

    /// <summary>The ISO 4217 code of a convertible currency to convert into.</summary>
    public string? To { get; set; }

    /// <summary>The day whose rate to use (nearest prior stored rate); <c>null</c> for the latest.</summary>
    public DateOnly? Date { get; set; }
}
