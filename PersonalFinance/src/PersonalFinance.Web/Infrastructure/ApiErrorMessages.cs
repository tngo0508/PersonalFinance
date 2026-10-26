using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Infrastructure.Localization;
using Refit;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Turns an ApiService error response into a message that is safe to show to the user, in the reader's language
/// (ADR 0017). A body is read as one of three shapes, in this order:
/// <list type="number">
/// <item>an <see cref="ApiErrorDto"/>: each entry rendered from its key;</item>
/// <item>ASP.NET's own validation <c>ProblemDetails</c>: a generic "invalid request", never its English text;</item>
/// <item>the older bodies of Features not converted yet (<c>{ "error": "..." }</c> and friends): their text as sent.</item>
/// </list>
/// </summary>
public static class ApiErrorMessages
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The message a page shows when a call to ApiService fails: ApiService's own message for a rejected request
    /// (4xx), "temporarily unavailable" when it cannot be reached, otherwise <paramref name="fallback"/> (text the
    /// caller has already localized). Never the exception text.
    /// </summary>
    public static string ForUser(IStringLocalizer localizer, Exception ex, string fallback)
    {
        return ex switch
        {
            ApiException api when (int)api.StatusCode < 500 => FromApiException(localizer, api),
            HttpRequestException or TimeoutException or TaskCanceledException => localizer["Errors.Unavailable"].Value,
            _ => fallback
        };
    }

    /// <summary>
    /// The user-facing message of an ApiService error response. Never raw HTML, a non-JSON payload or a 5xx body.
    /// </summary>
    public static string FromApiException(IStringLocalizer localizer, ApiException ex)
    {
        if ((int)ex.StatusCode >= 500)
        {
            return localizer["Errors.Server"].Value;
        }

        if (!string.IsNullOrWhiteSpace(ex.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(ex.Content);
                JsonElement root = doc.RootElement;
                if (TryParse(root, out ApiErrorDto? dto))
                {
                    return string.Join("; ", dto.Errors.Select(entry => Resolve(localizer, entry)));
                }

                if (IsProblemDetails(root))
                {
                    return localizer["Errors.InvalidRequest"].Value;
                }

                if (LegacyMessage(root) is { } legacy)
                {
                    return legacy;
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException)
            {
                return localizer["Errors.Generic"].Value;
            }
        }

        return localizer[ex.StatusCode switch
        {
            HttpStatusCode.NotFound => "Errors.NotFound",
            HttpStatusCode.Conflict => "Errors.Conflict",
            HttpStatusCode.BadRequest => "Errors.InvalidRequest",
            _ => "Errors.Generic"
        }].Value;
    }

    /// <summary>
    /// The resolved messages of a keyed rejection, grouped by <see cref="ApiErrorEntryDto.Field"/>; errors about the
    /// whole request are under <c>""</c>. Empty when the body is not an <see cref="ApiErrorDto"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields(IStringLocalizer localizer, ApiException ex)
    {
        if ((int)ex.StatusCode >= 500 || !TryParse(ex.Content, out ApiErrorDto? dto))
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }

        return dto.Errors
            .GroupBy(entry => entry.Field ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                IReadOnlyList<string> (group) => group.Select(entry => Resolve(localizer, entry)).ToList(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads <paramref name="content"/> as an <see cref="ApiErrorDto"/>: true only when <c>errors</c> is a non-empty
    /// array whose every item is an object with a <c>key</c> string.
    /// </summary>
    public static bool TryParse(string? content, [NotNullWhen(true)] out ApiErrorDto? dto)
    {
        dto = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return TryParse(doc.RootElement, out dto);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The text of <paramref name="entry"/> in the current UI culture, its arguments formatted for the current culture.
    /// A key no language has falls back to the entry's English <see cref="ApiErrorEntryDto.Message"/>.
    /// </summary>
    public static string Resolve(IStringLocalizer localizer, ApiErrorEntryDto entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Key))
        {
            return entry.Message;
        }

        LocalizedString template = localizer[entry.Key];
        if (template.ResourceNotFound)
        {
            return entry.Message;
        }

        if (entry.Args is not { Count: > 0 } args)
        {
            return template.Value;
        }

        CultureInfo culture = CultureInfo.CurrentCulture;
        Dictionary<string, object?> formatted = args.ToDictionary(pair => pair.Key,
            object? (pair) => ApiErrorArgFormat.Format(pair.Value, culture), StringComparer.Ordinal);
        return LocalizerExtensions.FormatNamed(template.Value, formatted);
    }

    private static bool TryParse(JsonElement root, [NotNullWhen(true)] out ApiErrorDto? dto)
    {
        dto = null;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("errors", out JsonElement errors) ||
            errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0 ||
            !errors.EnumerateArray().All(IsKeyedEntry))
        {
            return false;
        }

        dto = root.Deserialize<ApiErrorDto>(JsonOptions);
        return dto is not null;
    }

    private static bool IsKeyedEntry(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("key", out JsonElement key) &&
        key.ValueKind == JsonValueKind.String;

    /// <summary>ASP.NET's own model-validation answer: a <c>title</c> with an <c>errors</c> object, or a <c>type</c> URI.</summary>
    private static bool IsProblemDetails(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        ((root.TryGetProperty("title", out _) &&
          root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object) ||
         (root.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String));

    /// <summary>
    /// The text of a body from a Feature not converted to <see cref="ApiErrorDto"/> yet: <c>error</c>, then the
    /// messages of <c>errors</c>, then <c>detail</c>, <c>message</c> and <c>title</c>.
    /// </summary>
    private static string? LegacyMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (Text(root, "error") is { } error)
        {
            return error;
        }

        if (root.TryGetProperty("errors", out JsonElement errors) && LegacyErrors(errors) is { Count: > 0 } list)
        {
            return string.Join("; ", list);
        }

        return Text(root, "detail") ?? Text(root, "message") ?? Text(root, "title");
    }

    /// <summary>The messages of an <c>errors</c> array (strings or message objects) or object of arrays.</summary>
    private static List<string> LegacyErrors(JsonElement errors)
    {
        var messages = new List<string>();
        if (errors.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in errors.EnumerateArray())
            {
                string? message = item.ValueKind switch
                {
                    JsonValueKind.String => NonBlank(item.GetString()),
                    JsonValueKind.Object => Text(item, "errorMessage") ?? Text(item, "message"),
                    _ => null
                };
                if (message is not null)
                {
                    messages.Add(message);
                }
            }
        }
        else if (errors.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in errors.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    messages.AddRange(property.Value.EnumerateArray()
                        .Select(item => NonBlank(item.GetString()))
                        .OfType<string>());
                }
                else if (property.Value.ValueKind == JsonValueKind.String &&
                         NonBlank(property.Value.GetString()) is { } single)
                {
                    messages.Add(single);
                }
            }
        }

        return messages;
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) ? NonBlank(value.GetString()) : null;

    private static string? NonBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
