using System.Diagnostics.CodeAnalysis;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// What a Company Profile lookup accepts, shared by the Web page and the ApiService endpoints so both reject the same
/// input: a ticker of 1 to 10 letters, digits and dashes, and a search query of 1 to 40 characters.
/// </summary>
public static class CompanyLookupRules
{
    /// <summary>The longest ticker accepted.</summary>
    public const int MaxTickerLength = 10;

    /// <summary>The longest search query accepted.</summary>
    public const int MaxQueryLength = 40;

    /// <summary>
    /// The ticker in the form SEC lists it: trimmed, upper case, with a dot read as a dash (<c>brk.b</c> becomes
    /// <c>BRK-B</c>). False when it is empty, too long, or holds anything but letters, digits and dashes.
    /// </summary>
    public static bool TryNormalizeTicker(string? ticker, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        string candidate = (ticker ?? string.Empty).Trim().ToUpperInvariant().Replace('.', '-');
        if (candidate.Length is 0 or > MaxTickerLength || !char.IsAsciiLetterOrDigit(candidate[0]))
        {
            return false;
        }

        foreach (char c in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return false;
            }
        }

        normalized = candidate;
        return true;
    }

    /// <summary>
    /// The search query trimmed. False when it is empty or longer than <see cref="MaxQueryLength"/>.
    /// </summary>
    public static bool TryNormalizeQuery(string? query, [NotNullWhen(true)] out string? normalized)
    {
        string candidate = (query ?? string.Empty).Trim();
        normalized = candidate.Length is 0 or > MaxQueryLength ? null : candidate;
        return normalized is not null;
    }
}
