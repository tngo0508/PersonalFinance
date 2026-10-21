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
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // 1. Direct single error property
                    if (root.TryGetProperty("error", out var errorProp) && errorProp.GetString() is { } errorMsg &&
                        !string.IsNullOrWhiteSpace(errorMsg))
                    {
                        return errorMsg;
                    }

                    // 2. Errors collection (array of messages or object dictionary of arrays)
                    if (root.TryGetProperty("errors", out var errorsProp))
                    {
                        var errorList = new List<string>();

                        if (errorsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in errorsProp.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } str &&
                                    !string.IsNullOrWhiteSpace(str))
                                {
                                    errorList.Add(str);
                                }
                                else if (item.ValueKind == JsonValueKind.Object)
                                {
                                    if (item.TryGetProperty("errorMessage", out var em) &&
                                        em.GetString() is { } emStr && !string.IsNullOrWhiteSpace(emStr))
                                    {
                                        errorList.Add(emStr);
                                    }
                                    else if (item.TryGetProperty("message", out var m) && m.GetString() is { } mStr &&
                                             !string.IsNullOrWhiteSpace(mStr))
                                    {
                                        errorList.Add(mStr);
                                    }
                                }
                            }
                        }
                        else if (errorsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in errorsProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
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
                    if (root.TryGetProperty("detail", out var detailProp) && detailProp.GetString() is { } detailMsg &&
                        !string.IsNullOrWhiteSpace(detailMsg))
                    {
                        return detailMsg;
                    }

                    // 4. Message property
                    if (root.TryGetProperty("message", out var messageProp) &&
                        messageProp.GetString() is { } messageMsg && !string.IsNullOrWhiteSpace(messageMsg))
                    {
                        return messageMsg;
                    }

                    // 5. Title property
                    if (root.TryGetProperty("title", out var titleProp) && titleProp.GetString() is { } titleMsg &&
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
