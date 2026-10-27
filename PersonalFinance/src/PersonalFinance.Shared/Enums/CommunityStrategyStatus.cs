using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Where a Strategy is in the Admin's review (GLOSSARY.md). A new Strategy starts as <see cref="Pending"/>; only
/// <see cref="Approved"/> ones reach other users, while the author sees their own in every status (ADR 0018).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommunityStrategyStatus
{
    /// <summary>
    /// Waiting for an Admin; only its author sees it.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// On the board for everyone.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Turned down by an Admin; only its author sees it.
    /// </summary>
    Rejected = 3
}
