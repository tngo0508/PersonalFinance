using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>A fixed yearly rate a Money Priorities balance is projected at.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriorityGrowthScenario
{
    /// <summary>High-yield savings or short-term Treasuries.</summary>
    Cash = 1,

    /// <summary>Conservative investment return.</summary>
    Conservative = 2,

    /// <summary>Base investment return.</summary>
    Base = 3,

    /// <summary>Aggressive investment return.</summary>
    Aggressive = 4
}
