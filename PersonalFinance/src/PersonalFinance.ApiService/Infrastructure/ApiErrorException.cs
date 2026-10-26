using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     A rejection a service raises when the request cannot be honoured (ADR 0017). <see cref="ApiErrorExceptionFilter"/>
///     renders it as an <see cref="ApiErrorDto"/> with <see cref="StatusCode"/>, so the Web app can show the message in
///     the reader's language.
/// </summary>
/// <param name="key">The localization key, one of the <c>ApiErrorKeys</c> constants.</param>
/// <param name="message">The English message, used when the Web app has no translation for <paramref name="key"/>.</param>
/// <param name="field">The request field the error is about, or <c>null</c> for the whole request.</param>
/// <param name="args">The named arguments of the message, usually built with <see cref="ErrorArgs"/>.</param>
/// <param name="statusCode">The HTTP status of the response; 400 by default.</param>
public sealed class ApiErrorException(
    string key,
    string message,
    string? field = null,
    IReadOnlyDictionary<string, ApiErrorArgDto>? args = null,
    int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    /// <summary>The localization key of the message.</summary>
    public string Key { get; } = key;

    /// <summary>The request field the error is about, or <c>null</c> for the whole request.</summary>
    public string? Field { get; } = field;

    /// <summary>The named arguments of the message, or <c>null</c> when it has none.</summary>
    public IReadOnlyDictionary<string, ApiErrorArgDto>? Args { get; } = args;

    /// <summary>The HTTP status of the response.</summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>The response body: one entry carrying this error's key, message, field and arguments.</summary>
    public ApiErrorDto ToDto() => new()
    {
        Errors =
        [
            new ApiErrorEntryDto
            {
                Key = Key,
                Message = Message,
                Field = Field,
                Args = Args is null ? null : new Dictionary<string, ApiErrorArgDto>(Args)
            }
        ]
    };
}
