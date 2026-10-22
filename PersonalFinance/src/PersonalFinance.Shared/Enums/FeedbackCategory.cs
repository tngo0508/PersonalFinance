using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Enums;

/// <summary>
/// The kind of Feedback an Account sends.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeedbackCategory
{
    /// <summary>
    /// Something in the app is broken or behaves unexpectedly.
    /// </summary>
    Bug = 1,

    /// <summary>
    /// A suggestion for a new feature or an improvement.
    /// </summary>
    Idea = 2,

    /// <summary>
    /// Anything that is neither a Bug nor an Idea.
    /// </summary>
    Other = 3
}
