using System.Globalization;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// Sets both <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> for a <c>using</c>
/// block, the way request localization does (<c>vi</c> → <c>vi-VN</c>, spec §2.2), and restores them after.
/// </summary>
internal sealed class RequestCultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public RequestCultureScope(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture.Split('-')[0]);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
