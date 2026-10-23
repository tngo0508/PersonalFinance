using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
/// Dependency-injection registrations of the Spreadsheet Analysis Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class AnalysisFeature
{
    /// <summary>Spreadsheet Analysis: the analysis dashboard and the Google Sheet sync.</summary>
    public static IServiceCollection AddAnalysisFeature(this IServiceCollection services)
    {
        services.AddScoped<ISpreadsheetAnalysisService, SpreadsheetAnalysisService>();
        return services;
    }
}
