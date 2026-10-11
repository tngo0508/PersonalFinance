namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Response containing metadata for a created Monthly Budget Google Sheet template.
/// </summary>
public class CreateMonthlyBudgetTemplateResponseDto
{
    /// <summary>
    /// Indicates whether the template was successfully created and registered.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Google Drive file ID of the newly created spreadsheet.
    /// </summary>
    public string? FileId { get; set; }

    /// <summary>
    /// Display name of the spreadsheet file.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Direct URL to open and edit the spreadsheet in Google Sheets.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// Google Drive parent folder ID.
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// Error message if creation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Optional informational notice for a successful creation (e.g. the file was placed in My Drive root instead of the folder).
    /// </summary>
    public string? Notice { get; set; }

    /// <summary>
    /// Indicates whether sample demo data was generated (e.g. preview mode without live API key).
    /// </summary>
    public bool IsDemoData { get; set; }

    /// <summary>
    /// Indicates the user must grant Google Drive write access before the spreadsheet can be created.
    /// </summary>
    public bool RequiresAuthorization { get; set; }

    /// <summary>
    /// URL that starts the Google Drive authorization (OAuth consent) flow when <see cref="RequiresAuthorization"/> is true.
    /// </summary>
    public string? AuthorizationUrl { get; set; }

    /// <summary>
    /// Timestamp when the file was instantiated.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
