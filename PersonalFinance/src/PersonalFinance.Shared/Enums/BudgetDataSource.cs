using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Enum representing which stored budget data Analysis and the Dashboard read from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BudgetDataSource
{
    /// <summary>
    /// Synced Google Drive budget sheet data (Spreadsheet* tables).
    /// </summary>
    GoogleSheet = 1,

    /// <summary>
    /// Budget Tracker data entered in the application.
    /// </summary>
    BudgetTracker = 2
}
