using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// Checks the values a Research page reads from its address before they go back into the page or a Retry link.
/// </summary>
public static partial class ResearchAddress
{
    /// <summary>
    /// Whether <paramref name="basket"/> (already trimmed) has the shape of a Fund Basket, e.g. <c>SPY:60,QQQ:40</c>
    /// or <c>SPY,QQQ</c>: tickers, weights, colons and commas only, at most 200 characters. The funds themselves are
    /// checked in the page.
    /// </summary>
    public static bool IsWellFormedBasket([NotNullWhen(true)] string? basket) => basket is not null && BasketShape().IsMatch(basket);

    /// <summary>The most characters a country name in the address may have.</summary>
    public const int MaxCountryLength = 80;

    /// <summary>
    /// <paramref name="country"/> trimmed, or null unless it has the shape of a country name: 1 to
    /// <see cref="MaxCountryLength"/> letters, digits, spaces and the punctuation World Bank names use
    /// (<c>Korea, Rep.</c>, <c>Côte d'Ivoire</c>, <c>Hong Kong SAR (China)</c>). The name itself is matched in the
    /// page, so an unknown one selects nothing.
    /// </summary>
    public static string? NormalizeCountry(string? country)
    {
        string? trimmed = country?.Trim();
        return trimmed is { Length: > 0 and <= MaxCountryLength } && CountryShape().IsMatch(trimmed) ? trimmed : null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9.:,\-]{1,200}$", RegexOptions.CultureInvariant)]
    private static partial Regex BasketShape();

    [GeneratedRegex(@"^[\p{L}\p{M}0-9 .,'’()&\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryShape();
}
