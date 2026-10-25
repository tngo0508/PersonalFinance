using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
/// Dependency-injection registrations of the Spreadsheet Analysis Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class AnalysisFeature
{
    /// <summary>Spreadsheet Analysis: the analysis dashboard, the Google Sheet sync and its removal.</summary>
    public static IServiceCollection AddAnalysisFeature(this IServiceCollection services)
    {
        services.AddScoped<ISpreadsheetAnalysisService, SpreadsheetAnalysisService>();
        services.AddScoped<IGoogleSheetRemovalService, GoogleSheetRemovalService>();
        return services;
    }
}
