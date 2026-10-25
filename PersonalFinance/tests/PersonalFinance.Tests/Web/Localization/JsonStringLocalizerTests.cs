using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>
/// The fallback chain of spec §2.1: <c>CurrentUICulture → Parent → en</c>, <c>ResourceNotFound</c>, and events 3401/3402.
/// </summary>
public class JsonStringLocalizerTests
{
    private const string En = """{ "Common.Save": "Save", "Common.OnlyEnglish": "Only English", "Common.Hello": "Hello {0}" }""";
    private const string Vi = """{ "Common.Save": "Lưu", "Common.Hello": "Xin chào {0}" }""";

    [Fact]
    public void KeyTranslated_ReturnsTranslation_WithoutLogging()
    {
        var logger = new EventRecordingLogger();
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi, logger);
        using var culture = new UiCultureScope("vi");

        LocalizedString result = localizer["Common.Save"];

        Assert.Equal("Lưu", result.Value);
        Assert.False(result.ResourceNotFound);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void RegionalCulture_FallsBackToItsParentLanguage()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("vi-VN");

        Assert.Equal("Lưu", localizer["Common.Save"].Value);
    }

    [Fact]
    public void KeyOnlyInEnglish_ReturnsEnglish_FoundAndLogsFallbackEvent()
    {
        var logger = new EventRecordingLogger();
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi, logger);
        using var culture = new UiCultureScope("vi");

        LocalizedString result = localizer["Common.OnlyEnglish"];

        Assert.Equal("Only English", result.Value);
        Assert.False(result.ResourceNotFound);
        (LogLevel level, EventId eventId, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal(3401, eventId.Id);
        Assert.Equal("LocalizationFallback", eventId.Name);
        Assert.Contains("Common.OnlyEnglish", message);
    }

    [Fact]
    public void EnglishRequest_DoesNotLogFallback()
    {
        var logger = new EventRecordingLogger();
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi, logger);
        using var culture = new UiCultureScope("en-US");

        Assert.Equal("Only English", localizer["Common.OnlyEnglish"].Value);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void KeyNowhere_ReturnsKey_NotFoundAndLogsWarningEvent()
    {
        var logger = new EventRecordingLogger();
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi, logger);
        using var culture = new UiCultureScope("vi");

        LocalizedString result = localizer["Common.Missing"];

        Assert.Equal("Common.Missing", result.Value);
        Assert.True(result.ResourceNotFound);
        (LogLevel level, EventId eventId, string _) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Equal(3402, eventId.Id);
        Assert.Equal("LocalizationNotFound", eventId.Name);
    }

    [Fact]
    public void UnsupportedCulture_FallsBackToEnglish()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("fr-FR");

        Assert.Equal("Save", localizer["Common.Save"].Value);
    }

    [Fact]
    public void TextThatIsNotAKey_IsReturnedAsIs_WithoutWarning()
    {
        var logger = new EventRecordingLogger();
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi, logger);
        using var culture = new UiCultureScope("vi");

        LocalizedString result = localizer["The {0} field is required.", "Name"];

        Assert.Equal("The Name field is required.", result.Value);
        Assert.True(result.ResourceNotFound);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void PositionalArguments_AreFormattedIntoTheTranslation()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("vi");

        Assert.Equal("Xin chào An", localizer["Common.Hello", "An"].Value);
    }

    [Fact]
    public void GetAllStrings_ResolvesEveryKeyThroughTheChain()
    {
        JsonStringLocalizer localizer = LocalizationTestCatalog.Localizer(En, Vi);
        using var culture = new UiCultureScope("vi");

        Dictionary<string, string> all = localizer.GetAllStrings(includeParentCultures: true)
            .ToDictionary(s => s.Name, s => s.Value);

        Assert.Equal("Lưu", all["Common.Save"]);
        Assert.Equal("Only English", all["Common.OnlyEnglish"]);
    }

    [Fact]
    public void Chain_IsCultureThenParentsThenEnglish()
    {
        Assert.Equal(["vi-VN", "vi", "en"], JsonLocalizationCatalog.Chain(CultureInfo.GetCultureInfo("vi-VN")));
        Assert.Equal(["en-US", "en"], JsonLocalizationCatalog.Chain(CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void Catalog_ReadsOnlyLanguageFiles_IgnoringLockAndGlossary()
    {
        string folder = LocalizationTestCatalog.WriteFolder(En, Vi);
        File.WriteAllText(Path.Combine(folder, "i18n.lock.json"), """{ "vi": { "Common.Save": "abc" } }""");
        File.WriteAllText(Path.Combine(folder, "glossary.vi.json"), """{ "budget": "ngân sách" }""");

        var catalog = new JsonLocalizationCatalog(folder, ["en", "vi"]);

        Assert.Equal(3, catalog.Keys("en").Count);
        Assert.Equal(2, catalog.Keys("vi").Count);
    }

    [Fact]
    public void Catalog_MissingLanguageFile_LoadsEmpty()
    {
        string folder = LocalizationTestCatalog.WriteFolder(En, Vi);
        File.Delete(Path.Combine(folder, "vi.json"));

        var catalog = new JsonLocalizationCatalog(folder, ["en", "vi"]);

        Assert.Empty(catalog.Keys("vi"));
    }

    [Fact]
    public void Catalog_MalformedJson_FailsAtStartup()
    {
        string folder = LocalizationTestCatalog.WriteFolder(En, """{ "Common.Save": "Lưu", }""");

        Assert.ThrowsAny<JsonException>(() => new JsonLocalizationCatalog(folder, ["en", "vi"]));
    }

    [Fact]
    public void Catalog_NonStringValue_FailsAtStartup()
    {
        string folder = LocalizationTestCatalog.WriteFolder(En, """{ "Common": { "Save": "Lưu" } }""");

        Assert.Throws<InvalidDataException>(() => new JsonLocalizationCatalog(folder, ["en", "vi"]));
    }
}
