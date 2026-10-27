namespace PersonalFinance.Shared.DTOs.NetWorth;

/// <summary>
/// Request parameters for computing a net worth projection.
/// </summary>
public sealed class NetWorthProjectionRequestDto
{
    /// <summary>
    /// The ISO 4217 currency of the amounts, one of <c>SupportedCurrencies</c>; null keeps the saved parameters'
    /// currency (US dollars for new ones).
    /// </summary>
    public string? CurrencyCode { get; set; }

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

    /// <summary>Base scenario annual return on investment percentage (e.g., 7.0 for 7%).</summary>
    public decimal BaseRoi { get; set; }

    /// <summary>Conservative scenario annual return on investment percentage.</summary>
    public decimal ConservativeRoi { get; set; }

    /// <summary>Aggressive scenario annual return on investment percentage.</summary>
    public decimal AggressiveRoi { get; set; }

    /// <summary>Aggressive scenario annual savings amount.</summary>
    public decimal AggressiveSavings { get; set; }
}
