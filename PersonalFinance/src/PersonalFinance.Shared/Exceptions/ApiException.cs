namespace PersonalFinance.Shared.Exceptions;

/// <summary>
/// Represents an exception that occurs during API client communication.
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// Gets the HTTP status code returned by the API, if available.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the response body content returned by the API, if available.
    /// </summary>
    public string? Content { get; }

    public ApiException()
    {
    }

    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ApiException(int statusCode, string message, string? content = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Content = content;
    }
}