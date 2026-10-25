using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// ASP.NET Core Identity's error messages ("Passwords must have at least one digit") in the request's language.
/// Identity has no localization hook, so every message is overridden here and read from the
/// <c>Identity.Error.&lt;Code&gt;</c> key (spec §2.1). The codes stay the framework's, so callers matching on
/// <see cref="IdentityError.Code"/> keep working. Registered with <c>AddErrorDescriber</c>.
/// </summary>
internal sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private readonly IStringLocalizer _localizer;

    public LocalizedIdentityErrorDescriber(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    public override IdentityError DefaultError() => Error(nameof(DefaultError));

    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure));

    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));

    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));

    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed));

    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated));

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), ("userName", userName));

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), ("email", email));

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), ("userName", userName));

    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), ("email", email));

    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), ("role", role));

    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), ("role", role));

    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword));

    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled));

    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), ("role", role));

    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), ("role", role));

    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), ("length", length));

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), ("uniqueChars", uniqueChars));

    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric));

    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit));

    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower));

    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper));

    private IdentityError Error(string code, params (string Name, object? Value)[] args) =>
        new() { Code = code, Description = _localizer.Named("Identity.Error." + code, args) };
}
