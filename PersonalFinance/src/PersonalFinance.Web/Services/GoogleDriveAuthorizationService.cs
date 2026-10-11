using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Manages per-user Google OAuth tokens that grant the app write access to Google Drive (drive.file scope).
/// </summary>
public interface IGoogleDriveAuthorizationService
{
    /// <summary>
    /// Indicates whether Google OAuth (client ID/secret) is configured, which is required to authorize Drive access.
    /// </summary>
    Task<bool> IsConfiguredAsync();

    /// <summary>
    /// Persists the tokens returned by the Google OAuth consent flow for the given user.
    /// </summary>
    Task StoreTokensAsync(string userId, IEnumerable<AuthenticationToken> tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a non-expired access token for the user, refreshing it when necessary; null if the user hasn't authorized Drive.
    /// </summary>
    Task<string?> GetValidAccessTokenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes stored Drive tokens (e.g. after Google rejects them).
    /// </summary>
    Task ClearTokensAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores Google Drive OAuth tokens in ASP.NET Core Identity's user token store, encrypted with Data Protection.
/// </summary>
public class GoogleDriveAuthorizationService : IGoogleDriveAuthorizationService
{
    /// <summary>
    /// Google OAuth scope that lets the app create files in the user's Drive and access files it created.
    /// </summary>
    public const string DriveFileScope = "https://www.googleapis.com/auth/drive.file";

    // Kept separate from the "Google" sign-in provider so Drive consent never interferes with account login
    private const string TokenProvider = "GoogleDrive";
    private const string AccessTokenName = "access_token";
    private const string RefreshTokenName = "refresh_token";
    private const string ExpiresAtName = "expires_at";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IOptionsMonitor<GoogleOptions> _googleOptions;
    private readonly IDataProtector _protector;
    private readonly ILogger<GoogleDriveAuthorizationService> _logger;

    public GoogleDriveAuthorizationService(
        HttpClient httpClient,
        UserManager<IdentityUser> userManager,
        IAuthenticationSchemeProvider schemeProvider,
        IOptionsMonitor<GoogleOptions> googleOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GoogleDriveAuthorizationService> logger)
    {
        _httpClient = httpClient;
        _userManager = userManager;
        _schemeProvider = schemeProvider;
        _googleOptions = googleOptions;
        _protector = dataProtectionProvider.CreateProtector("PersonalFinance.GoogleDriveTokens.v1");
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync()
    {
        return await _schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) != null;
    }

    public async Task StoreTokensAsync(string userId, IEnumerable<AuthenticationToken> tokens, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        var tokenList = tokens.ToList();
        var accessToken = tokenList.FirstOrDefault(t => t.Name == AccessTokenName)?.Value;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google did not return an access token.");
        }

        await SetProtectedTokenAsync(user, AccessTokenName, accessToken);

        var expiresAt = tokenList.FirstOrDefault(t => t.Name == ExpiresAtName)?.Value;
        if (!string.IsNullOrWhiteSpace(expiresAt))
        {
            await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, ExpiresAtName, expiresAt);
        }

        // Google only returns a refresh token on the first consent (or when prompt=consent); keep the existing one otherwise
        var refreshToken = tokenList.FirstOrDefault(t => t.Name == RefreshTokenName)?.Value;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await SetProtectedTokenAsync(user, RefreshTokenName, refreshToken);
        }
    }

    public async Task<string?> GetValidAccessTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var accessToken = await GetProtectedTokenAsync(user, AccessTokenName);
        var expiresAtRaw = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, ExpiresAtName);
        var isExpired = !DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
                        || expiresAt <= DateTimeOffset.UtcNow.Add(ExpiryBuffer);

        if (!string.IsNullOrWhiteSpace(accessToken) && !isExpired)
        {
            return accessToken;
        }

        var refreshToken = await GetProtectedTokenAsync(user, RefreshTokenName);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        return await RefreshAccessTokenAsync(user, refreshToken, cancellationToken);
    }

    public async Task ClearTokensAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, AccessTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, ExpiresAtName);
    }

    private async Task<string?> RefreshAccessTokenAsync(IdentityUser user, string refreshToken, CancellationToken cancellationToken)
    {
        var options = _googleOptions.Get(GoogleDefaults.AuthenticationScheme);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return null;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        try
        {
            using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token refresh failed with HTTP {StatusCode} for user {UserId}: {Body}", response.StatusCode, user.Id, body);

                // invalid_grant means the user revoked access or the refresh token expired; a new consent is required
                if (body.Contains("invalid_grant", StringComparison.Ordinal))
                {
                    await ClearTokensAsync(user.Id, cancellationToken);
                }

                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            var expiresIn = root.TryGetProperty("expires_in", out var expiresProp) && expiresProp.TryGetInt32(out var seconds)
                ? seconds
                : 3600;

            await SetProtectedTokenAsync(user, AccessTokenName, accessToken);
            await _userManager.SetAuthenticationTokenAsync(
                user,
                TokenProvider,
                ExpiresAtName,
                DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));

            return accessToken;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Google token refresh failed for user {UserId}.", user.Id);
            return null;
        }
    }

    private Task SetProtectedTokenAsync(IdentityUser user, string name, string value)
    {
        return _userManager.SetAuthenticationTokenAsync(user, TokenProvider, name, _protector.Protect(value));
    }

    private async Task<string?> GetProtectedTokenAsync(IdentityUser user, string name)
    {
        var stored = await _userManager.GetAuthenticationTokenAsync(user, TokenProvider, name);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(stored);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogWarning(ex, "Stored Google Drive token for user {UserId} could not be decrypted.", user.Id);
            return null;
        }
    }
}
