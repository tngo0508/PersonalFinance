namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// The Idle Sign-out limits (<c>IdleSignOut</c> in appsettings). The same values set the sign-in cookie's lifetime and
/// reach the browser script through the layout, so the two cannot disagree.
/// </summary>
public sealed class IdleSignOutOptions
{
    public const string SectionName = "IdleSignOut";

    /// <summary>Minutes with no user activity before the Account is signed out.</summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>Seconds before the sign-out at which the warning dialog opens.</summary>
    public int WarningSeconds { get; set; } = 120;

    public TimeSpan Timeout => TimeSpan.FromMinutes(TimeoutMinutes);

    /// <summary>True when both limits are positive and the warning opens after the timeout starts.</summary>
    public bool IsValid() => TimeoutMinutes > 0 && WarningSeconds > 0 && WarningSeconds < TimeoutMinutes * 60;
}
