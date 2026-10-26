using System.Globalization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Reads a typed amount in a culture, strictly (spec §2.2, §5): an optional leading minus, digits with group separators
/// only between groups of three, and at most one decimal separator followed by digits (or by nothing: <c>1.234,</c>). <c>1.234,56</c> is 1234.56 in
/// vi-VN; <c>1234.56</c> and <c>1.5</c> are rejected there, where a lenient parse reads 123456 and 15. No currency
/// symbol, exponent, parentheses or trailing sign. The same rules are <c>PFI18n.parseNumber</c> in <c>core/i18n.js</c>.
/// </summary>
internal static class CultureDecimalParser
{
    /// <summary>Parses <paramref name="text"/> (surrounding white space ignored) in <paramref name="culture"/>.</summary>
    public static bool TryParse(string? text, CultureInfo culture, out decimal value)
    {
        ArgumentNullException.ThrowIfNull(culture);
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        NumberFormatInfo format = culture.NumberFormat;
        string input = text.Trim();
        bool negative = input.StartsWith(format.NegativeSign, StringComparison.Ordinal);
        if (negative)
        {
            input = input[format.NegativeSign.Length..];
        }

        string[] parts = input.Split(format.NumberDecimalSeparator);
        if (parts.Length > 2)
        {
            return false;
        }

        string integerDigits = IntegerDigits(parts[0], format.NumberGroupSeparator);
        string? fraction = parts.Length == 2 ? parts[1] : null;
        if (integerDigits.Length == 0 && parts[0].Length > 0)
        {
            return false; // malformed grouping
        }

        if (fraction is not null && !fraction.All(char.IsAsciiDigit))
        {
            return false;
        }

        // A decimal separator with no digits yet ("1.234," while typing) is the integer; a lone separator is nothing.
        if (integerDigits.Length == 0 && string.IsNullOrEmpty(fraction))
        {
            return false;
        }

        if (fraction is { Length: 0 })
        {
            fraction = null;
        }

        string invariant = (negative ? "-" : string.Empty)
                           + (integerDigits.Length == 0 ? "0" : integerDigits)
                           + (fraction is null ? string.Empty : "." + fraction);
        return decimal.TryParse(invariant, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// The digits of the integer part: either plain digits, or groups of three after a first group of one to three.
    /// Empty when the part is empty or its grouping is malformed.
    /// </summary>
    private static string IntegerDigits(string integerPart, string groupSeparator)
    {
        if (integerPart.Length == 0)
        {
            return string.Empty;
        }

        string[] groups = integerPart.Split(groupSeparator);
        bool digitsOnly = groups.All(group => group.Length > 0 && group.All(char.IsAsciiDigit));
        if (!digitsOnly)
        {
            return string.Empty;
        }

        bool wellGrouped = groups.Length == 1
                           || (groups[0].Length <= 3 && groups.Skip(1).All(group => group.Length == 3));
        return wellGrouped ? string.Concat(groups) : string.Empty;
    }
}
