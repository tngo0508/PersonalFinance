using System.ComponentModel.DataAnnotations;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Web.Models;

/// <summary>
/// View model for Google Drive folder exploration page.
/// </summary>
public class GoogleDriveViewModel
{
    /// <summary>
    /// Google Drive folder URL or ID entered by the user.
    /// </summary>
    [Required(ErrorMessage = "Please enter a Google Drive folder link or folder ID.")]
    [Display(Name = "Google Drive Folder URL / ID")]
    public string FolderUrl { get; set; } = "https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link";

    /// <summary>
    /// Optional Google Cloud API Key with Google Drive API enabled.
    /// </summary>
    [Display(Name = "Google Cloud API Key (Optional)")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Holds the retrieved folder metadata and files list.
    /// </summary>
    public GoogleDriveFolderResponseDto? Response { get; set; }

    /// <summary>
    /// General user-facing error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates whether a query was executed in this request.
    /// </summary>
    public bool HasQueried { get; set; }
}
