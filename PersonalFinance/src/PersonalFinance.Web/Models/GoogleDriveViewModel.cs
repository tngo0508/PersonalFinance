using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for Google Drive management, connection switching, caching, and exploration.
/// </summary>
public class GoogleDriveViewModel
{
    /// <summary>
    /// Google Drive folder URL or ID entered by the user.
    /// </summary>
    [Display(Name = "Google Drive Folder URL / ID")]
    public string FolderUrl { get; set; } = "https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link";

    /// <summary>
    /// Optional friendly name for the connection (e.g., 'Work Invoices', 'Tax Receipts').
    /// </summary>
    [Display(Name = "Drive / Folder Name (Optional)")]
    public string? ConnectionName { get; set; }

    /// <summary>
    /// Optional Google Cloud API Key with Google Drive API enabled.
    /// </summary>
    [Display(Name = "Google Cloud API Key (Optional)")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// List of all saved Google Drive connections for the authenticated user.
    /// </summary>
    public List<GoogleDriveConnectionDto> Connections { get; set; } = new();

    /// <summary>
    /// Currently selected connection ID.
    /// </summary>
    public int? SelectedConnectionId { get; set; }

    /// <summary>
    /// Currently selected connection details.
    /// </summary>
    public GoogleDriveConnectionDto? SelectedConnection =>
        Connections.FirstOrDefault(c => c.Id == SelectedConnectionId);

    /// <summary>
    /// Holds the retrieved folder metadata and files list (from database cache or live sync).
    /// </summary>
    public GoogleDriveFolderResponseDto? Response { get; set; }

    /// <summary>
    /// General user-facing error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// User-facing success/status message (e.g., sync confirmation).
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Indicates whether the connect / setup form should be displayed.
    /// </summary>
    public bool ShowConnectForm { get; set; }

    /// <summary>
    /// Indicates whether a query was executed in this request.
    /// </summary>
    public bool HasQueried { get; set; }
}
