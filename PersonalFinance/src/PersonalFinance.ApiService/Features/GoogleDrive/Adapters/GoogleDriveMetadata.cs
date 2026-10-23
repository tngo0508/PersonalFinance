using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.ApiService.Features.GoogleDrive.Adapters;

/// <summary>
/// Pure helpers for Google Drive file metadata: folder links, file sizes, file types, and whether a file is a
/// "Monthly budget" spreadsheet the Budget Report can read.
/// </summary>
internal static class GoogleDriveMetadata
{
    private static readonly Regex FolderUrlRegex = new(
        @"(?:folders\/|id=|file\/d\/)([a-zA-Z0-9_-]{15,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the Google Drive Folder ID from a full link or returns the string if already an ID.
    /// Supports formats:
    /// - https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link
    /// - https://drive.google.com/drive/u/0/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// - https://drive.google.com/open?id=127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// - 127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// </summary>
    public static string? ExtractFolderId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string trimmed = input.Trim();

        // Match regex patterns
        Match match = FolderUrlRegex.Match(trimmed);
        if (match.Success && match.Groups[1].Value.Length >= 15)
        {
            return match.Groups[1].Value;
        }

        // If input contains no slashes or query params and is between 15 and 100 valid base64url characters
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_-]{15,100}$"))
        {
            return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Formats raw byte count into a human-readable string (e.g. 1.25 MB, 450 KB).
    /// </summary>
    public static string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "-";
        }

        if (bytes.Value == 0)
        {
            return "0 B";
        }

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes.Value;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }

    /// <summary>
    /// Resolves human-friendly file type description and Bootstrap badge color from MIME type and filename.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Over the ADR 0007 complexity ceiling before it was set; follow-up: Google Drive client seam.")]
    public static (string FileType, string BadgeClass, bool IsFolder) ResolveTypeInfo(string? mimeType,
        string? fileName)
    {
        string mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        if (mime == "application/vnd.google-apps.folder")
        {
            return ("Folder", "bg-primary", true);
        }

        if (mime == "application/vnd.google-apps.spreadsheet" || ext is ".xlsx" or ".xls" or ".csv")
        {
            return ("Spreadsheet", "bg-success", false);
        }

        if (mime == "application/vnd.google-apps.document" || ext is ".docx" or ".doc" or ".odt")
        {
            return ("Document", "bg-primary", false);
        }

        if (mime == "application/vnd.google-apps.presentation" || ext is ".pptx" or ".ppt")
        {
            return ("Presentation", "bg-warning text-dark", false);
        }

        if (mime == "application/pdf" || ext == ".pdf")
        {
            return ("PDF Document", "bg-danger", false);
        }

        if (mime.StartsWith("image/", StringComparison.Ordinal) || ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg")
        {
            return ("Image", "bg-info text-dark", false);
        }

        if (mime.StartsWith("video/", StringComparison.Ordinal) || ext is ".mp4" or ".mov" or ".avi" or ".mkv")
        {
            return ("Video", "bg-dark text-white", false);
        }

        if (mime.StartsWith("audio/", StringComparison.Ordinal) || ext is ".mp3" or ".wav" or ".ogg" or ".m4a")
        {
            return ("Audio", "bg-secondary", false);
        }

        if (mime is "application/zip" or "application/x-tar" or "application/x-rar-compressed"
                or "application/x-7z-compressed" ||
            ext is ".zip" or ".tar" or ".gz" or ".7z" or ".rar")
        {
            return ("Archive", "bg-secondary", false);
        }

        if (mime.StartsWith("text/", StringComparison.Ordinal) || ext is ".txt" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".sql")
        {
            return ("Text File", "bg-light text-dark border", false);
        }

        return ("File", "bg-secondary", false);
    }

    /// <summary>
    /// Checks whether a given file qualifies as a "Monthly budget" spreadsheet.
    /// Matches any spreadsheet whose name starts with "Monthly budget" (case-insensitive).
    /// </summary>
    public static bool IsMonthlyBudgetSpreadsheet(string? fileName, string? fileType, string? mimeType = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string trimmedName = fileName.Trim();
        if (!trimmedName.StartsWith("Monthly budget", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(fileType, "Spreadsheet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string ext = Path.GetExtension(trimmedName).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls" or ".csv" or ".gsheet")
        {
            return true;
        }

        string mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        if (mime is "application/vnd.google-apps.spreadsheet" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.ms-excel" or
            "text/csv")
        {
            return true;
        }

        return false;
    }
}
