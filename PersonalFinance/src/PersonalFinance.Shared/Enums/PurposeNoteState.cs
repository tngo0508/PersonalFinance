using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>What became of a request for a Priority Walk's Purpose Note (GLOSSARY.md).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PurposeNoteState
{
    /// <summary>The note is written (or copied from an identical Walk) and saved onto the Walk.</summary>
    Written = 1,

    /// <summary>The Walk has no Purpose, so no note is written; the page invites the user to add one.</summary>
    NoPurpose = 2,

    /// <summary>The AI is not configured or the call failed: the note is "not available right now".</summary>
    Unavailable = 3,

    /// <summary>The shared AI quota is spent; the message is the quota's own.</summary>
    QuotaReached = 4,

    /// <summary>The Purpose looks like a prompt-injection attempt, so the AI is not asked.</summary>
    Refused = 5
}
