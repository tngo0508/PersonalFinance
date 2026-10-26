namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The localization keys an <see cref="PersonalFinance.Shared.Contracts.ApiErrorEntryDto"/> carries (ADR 0017,
/// localized API errors spec §2.1), one nested class per Feature. Each value is the key of the message in the Web
/// app's <c>en.json</c>. A key is named <c>&lt;Feature&gt;.Validation.&lt;Rule&gt;</c> for a validator rule and
/// <c>&lt;Feature&gt;.Error.&lt;Rule&gt;</c> for any other rejection.
/// </summary>
public static class ApiErrorKeys
{
    /// <summary>The keys of the Money Priorities Feature's rejections.</summary>
    public static class MoneyPriorities
    {
    }

    /// <summary>The keys of the Community Feature's rejections.</summary>
    public static class Community
    {
    }
}
