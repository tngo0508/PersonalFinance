using System.Globalization;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// Single source of truth for the culture used to format money, numbers and dates (US dollars, en-US).
/// Chiseled container images set no locale, so .NET starts in the invariant culture and prints "¤1,234.56"
/// for currency. Call <see cref="ApplyDefault"/> at startup of every host so output is identical everywhere.
/// WARNING: en-US resolves only while ICU globalization data is present. If InvariantGlobalization is enabled
/// or the runtime image has no ICU, en-US silently falls back to the invariant culture and "¤" returns.
/// </summary>
public static class AppCulture
{
    /// <summary>
    /// Culture name applied as the process default.
    /// </summary>
    public const string DefaultCultureName = "en-US";

    /// <summary>
    /// The default application culture instance.
    /// </summary>
    public static CultureInfo Default => CultureInfo.GetCultureInfo(DefaultCultureName);

    /// <summary>
    /// Sets the process-wide default culture and UI culture, covering the current thread and every future thread
    /// (request threads, thread-pool work and background services).
    /// </summary>
    public static void ApplyDefault()
    {
        CultureInfo culture = Default;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
