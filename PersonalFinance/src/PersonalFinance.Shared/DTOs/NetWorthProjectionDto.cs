namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Represents a single year's net worth projection data.
/// </summary>
public sealed class NetWorthYearDto
{
    /// <summary>Calendar year of the projection.</summary>
    public int Year { get; init; }

    /// <summary>Age of the person in this year.</summary>
    public int Age { get; init; }

    /// <summary>Balance at the start of the year.</summary>
    public decimal StartingBalance { get; init; }

    /// <summary>Amount saved and contributed during the year.</summary>
    public decimal Savings { get; init; }

    /// <summary>Return on investment earned during the year.</summary>
    public decimal Roi { get; init; }

    /// <summary>Balance at the end of the year after savings and ROI.</summary>
    public decimal EndingBalance { get; init; }
}

/// <summary>
/// Represents a scenario configuration (Base, Conservative, Aggressive).
/// </summary>
public sealed class NetWorthScenarioDto
{
    /// <summary>Name of the scenario (e.g., "Base").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Annual return on investment percentage.</summary>
    public decimal RoiPercent { get; set; }

    /// <summary>Amount saved and invested each year.</summary>
    public decimal AnnualSavings { get; set; }

    /// <summary>Initial net worth balance.</summary>
    public decimal StartingBalance { get; set; }

    /// <summary>Age at the start of the projection.</summary>
    public int StartingAge { get; set; }

    /// <summary>Final net worth at retirement age.</summary>
    public decimal FinalNetWorth { get; set; }

    /// <summary>Total amount contributed (starting balance + all annual savings).</summary>
    public decimal TotalContributions { get; set; }

    /// <summary>Total interest earned (final net worth minus total contributions).</summary>
    public decimal TotalInterestEarned { get; set; }

    /// <summary>Year-by-year projection data.</summary>
    public List<NetWorthYearDto> Years { get; set; } = new();
}

/// <summary>
/// Complete net worth projection data including all scenarios and summary metrics.
/// </summary>
public sealed class NetWorthProjectionDto
{
    /// <summary>Current calendar year when the projection is computed.</summary>
    public int CurrentYear { get; set; }

    /// <summary>Current age of the person.</summary>
    public int CurrentAge { get; set; }

    /// <summary>Annual gross income.</summary>
    public decimal AnnualIncome { get; set; }

    /// <summary>Annual spending amount (used to compute FI target).</summary>
    public decimal AnnualSpending { get; set; }

    /// <summary>Financial independence target (annual spending × 25, using the 4% rule).</summary>
    public decimal FiTarget { get; set; }

    /// <summary>Base scenario projection.</summary>
    public NetWorthScenarioDto Base { get; set; } = new();

    /// <summary>Conservative scenario projection.</summary>
    public NetWorthScenarioDto Conservative { get; set; } = new();

    /// <summary>Aggressive scenario projection.</summary>
    public NetWorthScenarioDto Aggressive { get; set; } = new();
}
