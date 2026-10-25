using System.Globalization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// The UI languages the app speaks. Adding a language is a data change: an entry here, its
/// <c>Resources/i18n/&lt;code&gt;.json</c> and glossary, and its plural categories in
/// <see cref="LocalizerExtensions.PluralCategory"/> (spec §4).
/// </summary>
internal static class AppLanguages
{
    /// <summary>The language every lookup ends at, and the one anonymous visitors get when nothing else matches.</summary>
    public const string Default = "en";

    /// <summary>
    /// Every supported language: its code (the JSON file name and the UI culture), the formatting culture it maps to
    /// (spec §2.2, used once slice L2 adds <c>vi-VN</c> to the supported cultures) and its name in itself, which the
    /// switcher shows untranslated so a reader can always find their own language.
    /// </summary>
    public static readonly IReadOnlyList<AppLanguage> All =
    [
        new AppLanguage { Code = "en", FormattingCulture = "en-US", NativeName = "English" },
        new AppLanguage { Code = "vi", FormattingCulture = "vi-VN", NativeName = "Tiếng Việt" }
    ];

    /// <summary>The UI cultures handed to <c>RequestLocalizationOptions.SupportedUICultures</c>.</summary>
    public static IList<CultureInfo> SupportedUICultures =>
        All.Select(language => CultureInfo.GetCultureInfo(language.Code)).ToList();

    /// <summary>Finds a supported language by its code, ignoring case; <c>null</c> when it is not supported.</summary>
    public static AppLanguage? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(language => string.Equals(language.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A supported UI language.</summary>
internal sealed record AppLanguage
{
    /// <summary>Two-letter code: the UI culture and the resource file name (<c>vi</c>).</summary>
    public required string Code { get; init; }

    /// <summary>The culture for numbers, dates and input parsing (<c>vi-VN</c>).</summary>
    public required string FormattingCulture { get; init; }

    /// <summary>The language's name in itself (<c>Tiếng Việt</c>).</summary>
    public required string NativeName { get; init; }
}
