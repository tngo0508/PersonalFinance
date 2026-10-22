using System.Globalization;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// User-facing To-do messages, shared by the ApiService validators and the Web page.
/// </summary>
public static class TodoMessages
{
    /// <summary>Shown when the title is empty or only whitespace.</summary>
    public const string TitleRequired = "Title is required.";

    /// <summary>Shown when the trimmed title is longer than <see cref="TodoLimits.TitleMaxLength"/>.</summary>
    public static readonly string TitleTooLong = string.Create(CultureInfo.InvariantCulture,
        $"Title must be {TodoLimits.TitleMaxLength:N0} characters or fewer.");

    /// <summary>Shown when a request body is missing or cannot be read (for example a malformed due date).</summary>
    public const string InvalidRequest = "Enter a title and, optionally, a valid due date.";
}
