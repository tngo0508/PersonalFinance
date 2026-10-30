using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// The kinds of Reaction (GLOSSARY.md): supportive only, with no negative kind (ADR 0018). An Account gives each kind
/// at most once to a Strategy or a Comment, and toggles it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommunityReactionKind
{
    /// <summary>
    /// "Helpful": the reader learned something from it.
    /// </summary>
    Helpful = 1,

    /// <summary>
    /// "Cheering you on": encouragement for the author.
    /// </summary>
    CheeringYouOn = 2
}
