namespace PersonalFinance.Shared.DTOs.GoogleDrive;

/// <summary>
/// Request payload to connect and persist a Google Drive folder.
/// The owning Account is taken from the authenticated request, never from the payload.
/// </summary>
public class ConnectGoogleDriveRequestDto
{
    public string FolderUrl { get; set; } = string.Empty;

    public string? ConnectionName { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>
    /// The ISO 4217 currency of the amounts in the folder's spreadsheets, one of <c>SupportedCurrencies</c>; every
    /// imported row inherits it. Null keeps a reconnected folder's currency, or gives a new one the Account's Reporting
    /// Currency.
    /// </summary>
    public string? CurrencyCode { get; set; }
}
