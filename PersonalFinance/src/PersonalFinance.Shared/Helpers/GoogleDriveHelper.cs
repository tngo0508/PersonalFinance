using System.Text.RegularExpressions;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Utility functions for parsing Google Drive URLs, resolving MIME types, and formatting file sizes.
/// </summary>
public static class GoogleDriveHelper
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

        var trimmed = input.Trim();

        // Match regex patterns
        var match = FolderUrlRegex.Match(trimmed);
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
    public static (string FileType, string BadgeClass, bool IsFolder) ResolveTypeInfo(string? mimeType, string? fileName)
    {
        var mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

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

        if (mime.StartsWith("image/") || ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg")
        {
            return ("Image", "bg-info text-dark", false);
        }

        if (mime.StartsWith("video/") || ext is ".mp4" or ".mov" or ".avi" or ".mkv")
        {
            return ("Video", "bg-dark text-white", false);
        }

        if (mime.StartsWith("audio/") || ext is ".mp3" or ".wav" or ".ogg" or ".m4a")
        {
            return ("Audio", "bg-secondary", false);
        }

        if (mime is "application/zip" or "application/x-tar" or "application/x-rar-compressed" or "application/x-7z-compressed" ||
            ext is ".zip" or ".tar" or ".gz" or ".7z" or ".rar")
        {
            return ("Archive", "bg-secondary", false);
        }

        if (mime.StartsWith("text/") || ext is ".txt" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".sql")
        {
            return ("Text File", "bg-light text-dark border", false);
        }

        return ("File", "bg-secondary", false);
    }
}
