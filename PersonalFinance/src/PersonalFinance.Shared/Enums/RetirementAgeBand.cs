namespace PersonalFinance.Shared.Enums;

/// <summary>
/// The age range Money Priorities asks for when the horizon is over 10 years, so the stated IRS limits include the
/// right catch-up. A Priority Walk stores the member name (at most 16 characters).
/// </summary>
public enum RetirementAgeBand
{
    /// <summary>Under 50 in the tax year: no catch-up.</summary>
    Under50 = 1,

    /// <summary>50 to 59, or 64 and over, in the tax year: the age-50 catch-up.</summary>
    Age50To59Or64Up = 2,

    /// <summary>Attains 60, 61, 62 or 63 in the tax year: the higher SECURE 2.0 plan catch-up.</summary>
    Age60To63 = 3
}
