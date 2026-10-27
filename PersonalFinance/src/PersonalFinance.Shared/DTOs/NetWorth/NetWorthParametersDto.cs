namespace PersonalFinance.Shared.DTOs.NetWorth;

/// <summary>
/// Response DTO returned when loading saved net worth parameters from the database.
/// </summary>
public sealed class NetWorthParametersDto
{
    /// <summary>The ISO 4217 currency of the saved amounts.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

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
    public DateTime UpdatedAtUtc { get; set; }
}
