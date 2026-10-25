using System.Globalization;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentUICulture"/> for the length of a <c>using</c> block and restores it after, so a
/// localization test cannot leak its language into the next test on the same thread.
/// </summary>
internal sealed class UiCultureScope : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentUICulture;

    public UiCultureScope(string uiCulture)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
    }

    public void Dispose() => CultureInfo.CurrentUICulture = _original;
}
