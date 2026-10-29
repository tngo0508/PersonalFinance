using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// Where one Lesson of the Journey (GLOSSARY.md) stands for an Account. Only one Lesson is <see cref="Open"/> at a
/// time; a finished Lesson stays open for re-reading.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LessonStatus
{
    /// <summary>Not yet open: the previous Lesson is unfinished, or its next local midnight has not passed.</summary>
    Locked = 1,

    /// <summary>Today's Lesson: it can be read and its Check answered.</summary>
    Open = 2,

    /// <summary>Its Check was answered; it can be reopened and its Check retaken.</summary>
    Finished = 3
}
