using System.Text.Json.Serialization;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// The body of every 4xx rejection the ApiService returns (ADR 0017, localized API errors spec §2.1). The Web app
/// renders each entry from its <see cref="ApiErrorEntryDto.Key"/> in the reader's language.
/// </summary>
public class ApiErrorDto
{
    /// <summary>The reasons the request was rejected, in the order they were found.</summary>
    public List<ApiErrorEntryDto> Errors { get; set; } = [];
}

/// <summary>One reason a request was rejected.</summary>
public class ApiErrorEntryDto
{
    /// <summary>
    /// Localization key of the message (<c>MoneyPriorities.Validation.Purpose</c>), one of the
    /// <see cref="PersonalFinance.Shared.Constants.ApiErrorKeys"/> constants.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The English message, used when the Web app has no translation for <see cref="Key"/>.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The request field the error is about, or <c>null</c> when it is about the whole request.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; set; }

    /// <summary>The named arguments of <see cref="Key"/>, or <c>null</c> when the message has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ApiErrorArgDto>? Args { get; set; }
}

/// <summary>A named argument of an error message, typed so the Web app can format it for the reader's culture.</summary>
public class ApiErrorArgDto
{
    /// <summary>How <see cref="Value"/> is formatted.</summary>
    public ApiErrorArgKind Kind { get; set; }

    /// <summary>The value, culture-invariant (<c>10000000</c>, not <c>10,000,000</c>).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The ISO 4217 currency code of a <see cref="ApiErrorArgKind.Money"/> value, otherwise <c>null</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Currency { get; set; }
}

/// <summary>How an <see cref="ApiErrorArgDto"/> value is formatted. Serialized as its name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiErrorArgKind
{
    /// <summary>Shown as is.</summary>
    Text,

    /// <summary>An invariant number, formatted for the reader's culture.</summary>
    Number,

    /// <summary>An invariant amount, formatted as currency in <see cref="ApiErrorArgDto.Currency"/>.</summary>
    Money
}
