using System.Text.RegularExpressions;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// What the Guild Hall counts as a money amount (ADR 0018): a currency symbol or code, or a currency word, next to a
/// number ("$40", "40 USD", "2 triệu đồng", "1.200.000đ"). Percentages and plain counts ("a third", "30%", "4 days")
/// are not amounts. The Web form nudges with <see cref="Pattern"/> as the user types and ApiService flags Strategies
/// for the Admin with <see cref="ContainsAmount"/>; neither blocks a post.
/// </summary>
public static partial class MoneyAmountRule
{
    /// <summary>
    /// The rule as a regular expression both .NET and JavaScript read the same way, case-insensitively (JavaScript
    /// needs the <c>iu</c> flags): letters are matched with <c>\p{L}</c> rather than <c>\b</c>, which JavaScript
    /// limits to ASCII.
    /// </summary>
    public const string Pattern =
        @"[$€£¥₩₹₫]\s?\d"
        + @"|(?<!\p{L})(?:usd|vnd|eur|gbp)\s?\d"
        + @"|\d(?:[\d.,]*\d)?\s?(?:(?:k|m|thousand|million|nghìn|ngàn|triệu|tỷ)\s?)?"
        + @"(?:usd|vnd|eur|gbp|dollars?|bucks|euros?|pounds?|dong|đồng|đ)(?!\p{L})";

    /// <summary>Whether any of <paramref name="texts"/> holds a money amount.</summary>
    public static bool ContainsAmount(params string?[] texts) =>
        texts.Any(text => !string.IsNullOrEmpty(text) && AmountRegex().IsMatch(text));

    [GeneratedRegex(Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountRegex();
}
