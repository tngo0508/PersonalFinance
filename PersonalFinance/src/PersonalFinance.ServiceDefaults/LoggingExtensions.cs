using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Hosting;
using Serilog.Sinks.SystemConsole.Themes;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Serilog setup shared by Web and ApiService: async console output, a daily rolling file (30-day retention),
/// and forwarding to the other logging providers (OpenTelemetry) registered by <c>AddServiceDefaults()</c>.
/// Levels and overrides come from the <c>Serilog</c> configuration section. Messages are formatted with the current
/// culture, which each app pins to en-US with <c>AppCulture.ApplyDefault()</c>.
/// </summary>
public static class LoggingExtensions
{
    private const string ConsoleTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    private const string FileTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Console-only logger for startup, so failures before the host is built are still logged.
    /// Assign it to <see cref="Log.Logger"/> as the first statement of <c>Program.cs</c>.
    /// </summary>
    public static ReloadableLogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code, outputTemplate: ConsoleTemplate,
                formatProvider: CultureInfo.CurrentCulture))
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Replaces the bootstrap logger with the full pipeline. Log files are written to <c>logs/{logFilePrefix}-yyyyMMdd.log</c>.
    /// </summary>
    public static TBuilder AddAppLogging<TBuilder>(this TBuilder builder, string logFilePrefix)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((services, configuration) => configuration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code, outputTemplate: ConsoleTemplate,
                    formatProvider: CultureInfo.CurrentCulture))
                .WriteTo.Async(a => a.File(
                    path: Path.Combine("logs", $"{logFilePrefix}-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: FileTemplate,
                    formatProvider: CultureInfo.CurrentCulture)),
            writeToProviders: true);

        return builder;
    }
}
