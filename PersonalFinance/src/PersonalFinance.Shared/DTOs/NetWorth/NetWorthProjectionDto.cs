namespace PersonalFinance.Shared.DTOs.NetWorth;

/// <summary>
/// One year of a net worth projection: a year of growth and saving that ends with the person at <see cref="Age"/>,
/// so <see cref="EndingBalance"/> is the balance held at that age.
/// </summary>
public sealed class NetWorthYearDto
{
    /// <summary>The calendar year the person turns <see cref="Age"/> in.</summary>
    public int Year { get; init; }

    /// <summary>The age at which <see cref="EndingBalance"/> is held: one year after the previous row, or after today.</summary>
    public int Age { get; init; }

    /// <summary>Balance the year starts with: today's for the first row, the previous row's ending balance after.</summary>
    public decimal StartingBalance { get; init; }

    /// <summary>Amount saved and contributed during the year, added at its end.</summary>
    public decimal Savings { get; init; }

    /// <summary>Return on investment earned during the year on <see cref="StartingBalance"/>.</summary>
    public decimal Roi { get; init; }

    /// <summary>Balance at <see cref="Age"/>: <see cref="StartingBalance"/> plus <see cref="Savings"/> and <see cref="Roi"/>.</summary>
    public decimal EndingBalance { get; init; }

    /// <summary>Everything put in by the end of the year: the starting balance plus each year's savings so far.</summary>
    public decimal TotalContributions { get; init; }

    /// <summary>How much prices have risen by the end of the year at the assumed inflation: 1.03 after one year at 3%.</summary>
    public decimal InflationFactor { get; init; }

    /// <summary>The ending balance in today's money: <see cref="EndingBalance"/> over <see cref="InflationFactor"/>.</summary>
    public decimal RealEndingBalance { get; init; }
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

    /// <summary>
    /// The first age at which the balance reaches the FI Target: the current age when the starting balance already
    /// does, else the <see cref="NetWorthYearDto.Age"/> of the first projected year whose ending balance does;
    /// <c>null</c> when it is not reached by retirement age, when there is no FI Target, or when retirement age is not
    /// after current age.
    /// </summary>
    public int? FiReachedAge { get; set; }

    /// <summary>Final net worth in today's money.</summary>
    public decimal RealFinalNetWorth { get; set; }

    /// <summary>
    /// Like <see cref="FiReachedAge"/>, with each year's balance in today's money, so the FI Target keeps pace with
    /// inflation.
    /// </summary>
    public int? RealFiReachedAge { get; set; }

    /// <summary>
    /// Year-by-year projection data, from the age after the current age to retirement age; empty when retirement age
    /// is not after the current age. Today's balance is <see cref="StartingBalance"/> at <see cref="StartingAge"/>.
    /// </summary>
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

    /// <summary>The assumed yearly inflation the today's-money figures take out, in percent.</summary>
    public decimal InflationPercent { get; set; }

    /// <summary>Financial independence target (annual spending × 25, using the 4% rule).</summary>
    public decimal FiTarget { get; set; }

    /// <summary>The user's net worth: the entered starting balance.</summary>
    public decimal StartingBalance { get; set; }

    /// <summary>Planned annual savings.</summary>
    public decimal AnnualSavings { get; set; }

    /// <summary>Age the projection runs to.</summary>
    public int RetirementAge { get; set; }

    /// <summary>Savings Rate: annual savings over annual income, one decimal; <c>null</c> without income.</summary>
    public decimal? SavingsRatePercent { get; set; }

    /// <summary>Progress to FI: starting balance over the FI Target, uncapped; <c>null</c> without an FI Target.</summary>
    public decimal? ProgressToFiPercent { get; set; }

    /// <summary>
    /// The ISO 4217 currency of every amount in the projection: the Account's Reporting Currency, the inputs converted
    /// once at the latest rate (ADR 0016), or the inputs' own currency when no rate exists.
    /// </summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>How the inputs were converted, for the page's caption; null when they were already in the Reporting Currency.</summary>
    public Currency.ConversionDto? Conversion { get; set; }

    /// <summary>Base scenario projection.</summary>
    public NetWorthScenarioDto Base { get; set; } = new();

    /// <summary>Conservative scenario projection.</summary>
    public NetWorthScenarioDto Conservative { get; set; } = new();

    /// <summary>Aggressive scenario projection.</summary>
    public NetWorthScenarioDto Aggressive { get; set; } = new();

    /// <summary>What one change to the Base inputs does to the outcome, in <see cref="NetWorthLevers"/> order.</summary>
    public List<NetWorthLeverDto> Levers { get; set; } = new();
}

/// <summary>
/// The Base scenario re-run with one input changed, for the page's "what moves your date" levers.
/// </summary>
public sealed class NetWorthLeverDto
{
    /// <summary>Which input changed: one of <see cref="NetWorthLevers"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The size of the change: an amount in the projection's currency, or percentage points of return.</summary>
    public decimal Change { get; set; }

    /// <summary>The age the FI Target is reached with the change; null when it is not reached by retirement.</summary>
    public int? FiReachedAge { get; set; }

    /// <summary>Like <see cref="FiReachedAge"/>, in today's money.</summary>
    public int? RealFiReachedAge { get; set; }

    /// <summary>Final net worth with the change.</summary>
    public decimal FinalNetWorth { get; set; }

    /// <summary>Final net worth with the change, in today's money.</summary>
    public decimal RealFinalNetWorth { get; set; }
}

/// <summary>The keys of <see cref="NetWorthLeverDto"/>.</summary>
public static class NetWorthLevers
{
    /// <summary>Save a set amount more each year.</summary>
    public const string SaveMore = "SaveMore";

    /// <summary>Spend a set amount less each year, which lowers the FI Target.</summary>
    public const string SpendLess = "SpendLess";

    /// <summary>Earn one percentage point more return.</summary>
    public const string ReturnUp = "ReturnUp";

    /// <summary>Earn one percentage point less return.</summary>
    public const string ReturnDown = "ReturnDown";
}
