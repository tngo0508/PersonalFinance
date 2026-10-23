using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.GoogleDrive.Adapters;

/// <summary>
/// Chooses the Google Drive API key for a request: the Account's own key when it has one, otherwise the
/// application default from <c>GoogleDrive:ApiKey</c>.
/// </summary>
internal static class GoogleDriveApiKey
{
    private const string DefaultKeyConfig = "GoogleDrive:ApiKey";

    /// <summary>
    /// Returns <paramref name="accountKey"/> when set, else the configured default, else <c>null</c>.
    /// </summary>
    public static string? Resolve(string? accountKey, IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(accountKey)
            ? accountKey.Trim()
            : configuration[DefaultKeyConfig]?.Trim();
    }
}
