using Microsoft.Extensions.Logging;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.Web.Localization;

/// <summary>Writes small resource files to a temporary folder and loads them, so a test controls every key.</summary>
internal static class LocalizationTestCatalog
{
    public static string WriteFolder(string enJson, string viJson)
    {
        string folder = Directory.CreateTempSubdirectory("i18n-").FullName;
        File.WriteAllText(Path.Combine(folder, "en.json"), enJson);
        File.WriteAllText(Path.Combine(folder, "vi.json"), viJson);
        return folder;
    }

    public static JsonLocalizationCatalog Create(string enJson, string viJson) =>
        new(WriteFolder(enJson, viJson), ["en", "vi"]);

    public static JsonStringLocalizer Localizer(string enJson, string viJson, ILogger? logger = null) =>
        new(Create(enJson, viJson), logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
}

/// <summary>Records each log entry with its event, for the fallback and not-found events.</summary>
internal sealed class EventRecordingLogger : ILogger
{
    public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, eventId, formatter(state, exception)));
}
