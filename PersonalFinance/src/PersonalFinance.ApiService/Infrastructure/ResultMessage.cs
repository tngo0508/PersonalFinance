using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Builds the keyed entry of an error or warning message a successful result carries beside its English text (#405),
///     so the Web app can show it in the reader's language. A 4xx rejection is an <see cref="ApiErrorException"/> instead.
/// </summary>
public static class ResultMessage
{
    /// <summary>An entry for <paramref name="key"/>, one of the <c>ResultMessageKeys</c> constants.</summary>
    /// <param name="key">The localization key of the message.</param>
    /// <param name="message">The English message, used when the Web app has no translation for <paramref name="key"/>.</param>
    /// <param name="args">The named arguments of the message, usually built with <see cref="ErrorArgs"/>.</param>
    public static ApiErrorEntryDto Entry(string key, string message,
        IReadOnlyDictionary<string, ApiErrorArgDto>? args = null) => new()
    {
        Key = key,
        Message = message,
        Args = args is null ? null : new Dictionary<string, ApiErrorArgDto>(args)
    };
}
