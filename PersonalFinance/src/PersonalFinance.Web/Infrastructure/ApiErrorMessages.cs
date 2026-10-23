using System.Net;
using System.Text.Json;
using Refit;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// Turns an ApiService error response into a message that is safe to show to the user.
/// </summary>
public static class ApiErrorMessages
{
    /// <summary>
    /// The message a page shows when a call to ApiService fails: ApiService's own message for a rejected request
    /// (4xx), "temporarily unavailable" when it cannot be reached, otherwise <paramref name="fallback"/>.
    /// Never the exception text.
    /// </summary>
    public static string ForUser(Exception ex, string fallback)
    {
        return ex switch
        {
            ApiException api when (int)api.StatusCode < 500 => FromApiException(api),
            HttpRequestException or TimeoutException or TaskCanceledException => JsonApiErrorsFilter.UnavailableMessage,
            _ => fallback
        };
    }

    /// <summary>
    /// Extracts a user-friendly error message from an ApiException payload.
    /// Avoids exposing raw HTML, non-JSON backend payloads, or 5xx server internals.
    /// </summary>
    public static string FromApiException(ApiException ex)
    {
        if ((int)ex.StatusCode >= 500)
        {
            return "A server error occurred. Please try again.";
        }

        if (!string.IsNullOrWhiteSpace(ex.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(ex.Content);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // 1. Direct single error property
                    if (root.TryGetProperty("error", out JsonElement errorProp) && errorProp.GetString() is { } errorMsg &&
                        !string.IsNullOrWhiteSpace(errorMsg))
                    {
                        return errorMsg;
                    }

                    // 2. Errors collection (array of messages or object dictionary of arrays)
                    if (root.TryGetProperty("errors", out JsonElement errorsProp))
                    {
                        var errorList = new List<string>();

                        if (errorsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in errorsProp.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } str &&
                                    !string.IsNullOrWhiteSpace(str))
                                {
                                    errorList.Add(str);
                                }
                                else if (item.ValueKind == JsonValueKind.Object)
                                {
                                    if (item.TryGetProperty("errorMessage", out JsonElement em) &&
                                        em.GetString() is { } emStr && !string.IsNullOrWhiteSpace(emStr))
                                    {
                                        errorList.Add(emStr);
                                    }
                                    else if (item.TryGetProperty("message", out JsonElement m) && m.GetString() is { } mStr &&
                                             !string.IsNullOrWhiteSpace(mStr))
                                    {
                                        errorList.Add(mStr);
                                    }
                                }
                            }
                        }
                        else if (errorsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty prop in errorsProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement item in prop.Value.EnumerateArray())
                                    {
                                        if (item.GetString() is { } msg && !string.IsNullOrWhiteSpace(msg))
                                        {
                                            errorList.Add(msg);
                                        }
                                    }
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.String &&
                                         prop.Value.GetString() is { } singleMsg &&
                                         !string.IsNullOrWhiteSpace(singleMsg))
                                {
                                    errorList.Add(singleMsg);
                                }
                            }
                        }

                        if (errorList.Count > 0)
                        {
                            return string.Join("; ", errorList);
                        }
                    }

                    // 3. Detail property
                    if (root.TryGetProperty("detail", out JsonElement detailProp) && detailProp.GetString() is { } detailMsg &&
                        !string.IsNullOrWhiteSpace(detailMsg))
                    {
                        return detailMsg;
                    }

                    // 4. Message property
                    if (root.TryGetProperty("message", out JsonElement messageProp) &&
                        messageProp.GetString() is { } messageMsg && !string.IsNullOrWhiteSpace(messageMsg))
                    {
                        return messageMsg;
                    }

                    // 5. Title property
                    if (root.TryGetProperty("title", out JsonElement titleProp) && titleProp.GetString() is { } titleMsg &&
                        !string.IsNullOrWhiteSpace(titleMsg))
                    {
                        return titleMsg;
                    }
                }
            }
            catch
            {
                return "An error occurred while processing the request.";
            }
        }

        return ex.StatusCode switch
        {
            HttpStatusCode.NotFound => "The requested resource was not found.",
            HttpStatusCode.Conflict => "A conflict occurred while processing the request.",
            HttpStatusCode.BadRequest => "The request was invalid.",
            _ => "An error occurred while processing the request."
        };
    }
}
