namespace PersonalFinance.Data.Entities;

/// <summary>
/// Records user consent to Terms of Service and Privacy Policy.
/// </summary>
public class UserConsent : AuditableEntity
{
    /// <summary>
    /// Gets or sets the version of the policies the user consented to.
    /// </summary>
    public string PolicyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the user accepted the Terms of Service.
    /// </summary>
    public bool AcceptedTerms { get; set; }

    /// <summary>
    /// Gets or sets whether the user accepted the Privacy Policy.
    /// </summary>
    public bool AcceptedPrivacy { get; set; }

    /// <summary>
    /// Gets or sets the IP address at the time of consent.
    /// </summary>
    public string? IpAddress { get; set; }
}
