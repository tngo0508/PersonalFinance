using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// How often a Challenge (GLOSSARY.md) asks for a Check-in: one per day, or one per week counted from its start date.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommunityChallengePeriod
{
    /// <summary>One Check-in per local day.</summary>
    Daily = 1,

    /// <summary>One Check-in per seven days, the first week starting on the Challenge's start date.</summary>
    Weekly = 2
}
