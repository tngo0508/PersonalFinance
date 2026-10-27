using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database persistence entity representing a user's saved net worth projection parameters.
/// One record per user — each save overwrites the previous parameters.
/// </summary>
public class NetWorthParameters : AuditableEntity
{
    /// <summary>
    /// ISO 4217 code, one of <see cref="SupportedCurrencies"/> (ADR 0016). Every amount of these parameters is in this
    /// currency. Rows saved before amounts carried a currency are <c>USD</c>.
    /// </summary>
    public string CurrencyCode { get; set; } = SupportedCurrencies.DefaultCode;

    /// <summary>Initial net worth balance.</summary>
    public decimal StartingBalance { get; set; }

    /// <summary>Amount saved and invested each year.</summary>
    public decimal AnnualSavings { get; set; }

    /// <summary>Annual gross income.</summary>
    public decimal AnnualIncome { get; set; }

    /// <summary>Annual spending amount (used to compute FI target).</summary>
    public decimal AnnualSpending { get; set; }

    /// <summary>Current age of the person.</summary>
    public int CurrentAge { get; set; }

    /// <summary>Target retirement age.</summary>
    public int RetirementAge { get; set; }

    /// <summary>Base scenario annual return on investment percentage.</summary>
    public decimal BaseRoi { get; set; }

    /// <summary>Conservative scenario annual return on investment percentage.</summary>
    public decimal ConservativeRoi { get; set; }

    /// <summary>Aggressive scenario annual return on investment percentage.</summary>
    public decimal AggressiveRoi { get; set; }

    /// <summary>Aggressive scenario annual savings amount.</summary>
    public decimal AggressiveSavings { get; set; }

    /// <summary>Timestamp when these parameters were last saved.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
