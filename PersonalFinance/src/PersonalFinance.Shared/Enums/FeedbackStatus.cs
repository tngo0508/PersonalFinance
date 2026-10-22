using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Where a piece of Feedback is in the Admin's triage. New Feedback starts as <see cref="New"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeedbackStatus
{
    /// <summary>
    /// Not yet read by an Admin.
    /// </summary>
    New = 1,

    /// <summary>
    /// Read by an Admin.
    /// </summary>
    Reviewed = 2,

    /// <summary>
    /// Dealt with and hidden from the inbox by default.
    /// </summary>
    Archived = 3
}
