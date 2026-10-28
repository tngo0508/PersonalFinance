using Microsoft.Extensions.Logging;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>Hands one <see cref="RecordingLogger{T}"/> to every category, so a test sees what the code under test logs.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public RecordingLogger<RecordingLoggerProvider> Logger { get; } = new();

    public List<string> Messages => Logger.Entries.Select(e => e.Message).ToList();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose()
    {
    }
}
