namespace PersonalFinance.Shared.DTOs;

/// <summary>
/// Represents a single year's net worth projection data.
/// </summary>
public class NetWorthYearDto
{
    public int Year { get; set; }
    public int Age { get; set; }
    public decimal StartingBalance { get; set; }
    public decimal Savings { get; set; }
    public decimal Roi { get; set; }
    public decimal EndingBalance { get; set; }
}

/// <summary>
/// Represents a scenario configuration (Base, Conservative, Aggressive).
/// </summary>
public class NetWorthScenarioDto
{
    public string Name { get; set; } = string.Empty;
    public decimal RoiPercent { get; set; }
    public decimal AnnualSavings { get; set; }
    public decimal StartingBalance { get; set; }
    public int StartingAge { get; set; }
    public decimal FinalNetWorth { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalInterestEarned { get; set; }
    public List<NetWorthYearDto> Years { get; set; } = new();
}

/// <summary>
/// Complete net worth projection data including all scenarios and summary metrics.
/// </summary>
public class NetWorthProjectionDto
{
    public int CurrentYear { get; set; }
    public int CurrentAge { get; set; }
    public decimal AnnualIncome { get; set; }
    public decimal AnnualSpending { get; set; }
    public decimal FiTarget { get; set; }
    public NetWorthScenarioDto Base { get; set; } = new();
    public NetWorthScenarioDto Conservative { get; set; } = new();
    public NetWorthScenarioDto Aggressive { get; set; } = new();
}
