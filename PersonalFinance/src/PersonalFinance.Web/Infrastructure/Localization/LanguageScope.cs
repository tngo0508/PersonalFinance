using System.Globalization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Switches <see cref="CultureInfo.CurrentUICulture"/> to a Language for a <c>using</c> block, then restores it: how an
/// email is composed in its recipient's Language inside someone else's request (spec §2.3). Formatting
/// (<see cref="CultureInfo.CurrentCulture"/>) is left alone until slice L2.
/// </summary>
internal sealed class LanguageScope : IDisposable
{
    private readonly CultureInfo _previous;

    private LanguageScope(AppLanguage language)
    {
        _previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language.Code);
    }

    /// <summary>Uses <paramref name="language"/> until disposed.</summary>
    public static LanguageScope Use(AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return new LanguageScope(language);
    }

    /// <inheritdoc />
    public void Dispose() => CultureInfo.CurrentUICulture = _previous;
}
