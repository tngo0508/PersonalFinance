namespace PersonalFinance.ApiService.Features.Research.Adapters;

/// <summary>
/// How the ApiService calls SEC EDGAR (<c>Research:Sec</c> in configuration). SEC's fair-access rules ask every
/// automated caller to declare who it is in the User-Agent and to stay under 10 requests a second.
/// </summary>
internal sealed class SecEdgarSettings
{
    /// <summary>The configuration section the settings are read from.</summary>
    public const string Section = "Research:Sec";

    /// <summary>
    /// The User-Agent sent on every request: an application name and a contact email (<c>Research:Sec:UserAgent</c>).
    /// It comes from configuration, never from code, so no personal address is committed.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>The most requests sent to SEC in any one second; kept under SEC's limit of 10.</summary>
    public int MaxRequestsPerSecond { get; init; } = 5;
}
