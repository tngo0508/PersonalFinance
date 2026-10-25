using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Renders a Notification's title and message in the reader's language at read time (localization spec §2.3), from
/// the key and named arguments it was stored with. A Notification stored before keys existed, or whose key no
/// resource file has, keeps the English text it was stored with.
/// </summary>
internal static class NotificationText
{
    /// <summary>Replaces <paramref name="notification"/>'s title and message with their localized text, when it has keys.</summary>
    public static void Localize(IStringLocalizer localizer, NotificationDto notification)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(notification);

        IReadOnlyDictionary<string, object?> arguments = (notification.Arguments ?? [])
            .ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
        notification.Title = Render(localizer, notification.TitleKey, arguments) ?? notification.Title;
        notification.Message = Render(localizer, notification.MessageKey, arguments) ?? notification.Message;
    }

    private static string? Render(IStringLocalizer localizer, string? key, IReadOnlyDictionary<string, object?> arguments)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        LocalizedString template = localizer[key];
        return template.ResourceNotFound ? null : LocalizerExtensions.FormatNamed(template.Value, arguments);
    }
}
