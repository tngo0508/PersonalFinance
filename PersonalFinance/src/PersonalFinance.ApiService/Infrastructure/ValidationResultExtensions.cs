using FluentValidation.Results;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
///     Turns a failed FluentValidation result into the <see cref="ApiErrorDto"/> body (ADR 0017). Validators set the
///     key with <c>WithErrorCode</c> and the arguments with <c>WithState(_ =&gt; ErrorArgs...)</c>.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    ///     One entry per failure: <c>ErrorCode</c> is the key, <c>ErrorMessage</c> the English message,
    ///     <c>PropertyName</c> the field (<c>null</c> when empty) and an <see cref="ErrorArgs"/> dictionary in
    ///     <c>CustomState</c> the arguments.
    /// </summary>
    public static ApiErrorDto ToApiError(this ValidationResult result) => new()
    {
        Errors = [.. result.Errors.Select(ToEntry)]
    };

    private static ApiErrorEntryDto ToEntry(ValidationFailure failure) => new()
    {
        Key = failure.ErrorCode,
        Message = failure.ErrorMessage,
        Field = string.IsNullOrEmpty(failure.PropertyName) ? null : failure.PropertyName,
        Args = failure.CustomState is IReadOnlyDictionary<string, ApiErrorArgDto> args
            ? new Dictionary<string, ApiErrorArgDto>(args)
            : null
    };
}
