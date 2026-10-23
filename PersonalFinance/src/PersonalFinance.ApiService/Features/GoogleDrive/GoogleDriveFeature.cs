using PersonalFinance.ApiService.Features.GoogleDrive.Adapters;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.ApiService.Services;
using PersonalFinance.ApiService.Validators;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Dependency-injection registrations of the Google Drive Integration Feature (GLOSSARY.md), called from
/// <c>Program.cs</c> (ADR 0007).
/// </summary>
internal static class GoogleDriveFeature
{
    private static readonly TimeSpan GoogleDriveTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Google Drive connections, the Drive file source and spreadsheet reading.</summary>
    public static IServiceCollection AddGoogleDriveFeature(this IServiceCollection services)
    {
        services.AddHttpClient<IDriveConnectionService, DriveConnectionService>(client => client.Timeout = GoogleDriveTimeout);
        services.AddHttpClient<IGoogleDriveFileSource, GoogleDriveFileSource>(client => client.Timeout = GoogleDriveTimeout);
        services.AddScoped<ISpreadsheetReader, SpreadsheetReaderService>();
        return services;
    }
}
