using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.GoogleDrive;

/// <summary>
/// The Google Drive page's own wording in the request's Language (spec §2.1): file-type labels and how long ago a
/// drive was synced. The ApiService sends file types in English, and the page also filters on that English value, so
/// only the label is translated. Drive, folder and file names are the user's own and are never translated. The error
/// and warning messages of a Google Drive result are translated by their keyed entry (#405).
/// </summary>
public static class GoogleDriveText
{
    /// <summary>The English file types the ApiService sends, with their label.</summary>
    private static readonly Dictionary<string, Func<IStringLocalizer, string>> FileTypeLabels = new(StringComparer.Ordinal)
    {
        ["Folder"] = localizer => localizer["GoogleDrive.FileType.Folder"],
        ["Spreadsheet"] = localizer => localizer["GoogleDrive.FileType.Spreadsheet"],
        ["Document"] = localizer => localizer["GoogleDrive.FileType.Document"],
        ["Presentation"] = localizer => localizer["GoogleDrive.FileType.Presentation"],
        ["PDF Document"] = localizer => localizer["GoogleDrive.FileType.Pdf"],
        ["Image"] = localizer => localizer["GoogleDrive.FileType.Image"],
        ["Video"] = localizer => localizer["GoogleDrive.FileType.Video"],
        ["Audio"] = localizer => localizer["GoogleDrive.FileType.Audio"],
        ["Archive"] = localizer => localizer["GoogleDrive.FileType.Archive"],
        ["Text File"] = localizer => localizer["GoogleDrive.FileType.Text"],
        ["File"] = localizer => localizer["GoogleDrive.FileType.File"]
    };

    /// <summary>The label of a file type: translated when the page knows it, otherwise as sent.</summary>
    public static string FileType(IStringLocalizer localizer, string fileType)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return FileTypeLabels.TryGetValue(fileType, out Func<IStringLocalizer, string>? label)
            ? label(localizer)
            : fileType;
    }

    /// <summary>Translates a folder result's error and warning in place.</summary>
    public static GoogleDriveFolderResponseDto Localize(IStringLocalizer localizer, GoogleDriveFolderResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.ErrorMessage = ApiErrorMessages.Resolve(localizer, response.Error, response.ErrorMessage);
        response.WarningMessage = ApiErrorMessages.Resolve(localizer, response.Warning, response.WarningMessage);
        return response;
    }

    /// <summary>Translates a connection's error in place.</summary>
    public static GoogleDriveConnectionDto Localize(IStringLocalizer localizer, GoogleDriveConnectionDto connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.ErrorMessage = ApiErrorMessages.Resolve(localizer, connection.Error, connection.ErrorMessage);
        return connection;
    }

    /// <summary>Translates why a budget report could not be read, in place.</summary>
    public static MonthlyBudgetReportDto Localize(IStringLocalizer localizer, MonthlyBudgetReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        report.ErrorMessage = ApiErrorMessages.Resolve(localizer, report.Error, report.ErrorMessage);
        return report;
    }

    /// <summary>Translates why no transactions could be read, in place.</summary>
    public static SpreadsheetTransactionsDto Localize(IStringLocalizer localizer, SpreadsheetTransactionsDto transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        transactions.ErrorMessage =
            ApiErrorMessages.Resolve(localizer, transactions.Error, transactions.ErrorMessage);
        return transactions;
    }

    /// <summary>
    /// A date with a short month name in the request culture: "Mar 5, 2026" in English, "5 thg 3 2026" in Vietnamese.
    /// .NET has no CLDR medium date pattern, so the order is chosen per language, like the plural table.
    /// </summary>
    public static string ShortDate(DateTime date)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        return date.ToString(culture.TwoLetterISOLanguageName == "en" ? "MMM d, yyyy" : "d MMM yyyy", culture);
    }

    /// <summary>
    /// How long before <paramref name="nowUtc"/> a drive was synced: "just now", "5m ago", "3h ago", "2d ago", then
    /// the date in the request culture after a week.
    /// </summary>
    public static string TimeAgo(IStringLocalizer localizer, DateTime syncedUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        DateTime synced = syncedUtc.Kind == DateTimeKind.Local ? syncedUtc.ToUniversalTime() : syncedUtc;
        TimeSpan span = nowUtc - synced;

        if (span.TotalMinutes < 1)
        {
            return localizer["GoogleDrive.TimeAgo.JustNow"];
        }

        if (span.TotalMinutes < 60)
        {
            return localizer.Named("GoogleDrive.TimeAgo.Minutes", ("count", (int)span.TotalMinutes));
        }

        if (span.TotalHours < 24)
        {
            return localizer.Named("GoogleDrive.TimeAgo.Hours", ("count", (int)span.TotalHours));
        }

        if (span.TotalDays < 7)
        {
            return localizer.Named("GoogleDrive.TimeAgo.Days", ("count", (int)span.TotalDays));
        }

        return ShortDate(synced);
    }
}
