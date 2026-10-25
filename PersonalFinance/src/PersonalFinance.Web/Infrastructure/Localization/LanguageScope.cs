using System.Globalization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Switches <see cref="CultureInfo.CurrentUICulture"/> to a Language and <see cref="CultureInfo.CurrentCulture"/> to
/// its formatting culture for a <c>using</c> block, then restores both: how an email is composed in its recipient's
/// Language, numbers and dates inside someone else's request (spec §2.2–2.3).
/// </summary>
internal sealed class LanguageScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;

    private LanguageScope(AppLanguage language)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language.FormattingCulture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language.Code);
    }

    /// <summary>Uses <paramref name="language"/> until disposed.</summary>
    public static LanguageScope Use(AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return new LanguageScope(language);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
