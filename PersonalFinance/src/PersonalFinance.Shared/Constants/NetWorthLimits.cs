namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The ranges the Net Worth projection amounts must fall in, shared by the Web form, the ApiService validator and the
/// page's scripts. Written in whole US dollars; <see cref="Helpers.MoneyBounds"/> scales each to the currency the
/// amounts are entered in (#444). Every amount is also at least 0. The ages and rates keep their own ranges in the
/// form and the validator.
/// </summary>
public static class NetWorthLimits
{
    /// <summary>The largest starting balance, in US dollars.</summary>
    public const long StartingBalanceMax = 1_000_000_000;

    /// <summary>The largest annual savings, in US dollars.</summary>
    public const long AnnualSavingsMax = 10_000_000;

    /// <summary>The largest annual income, in US dollars.</summary>
    public const long AnnualIncomeMax = 100_000_000;

    /// <summary>The largest annual spending, in US dollars.</summary>
    public const long AnnualSpendingMax = 10_000_000;

    /// <summary>The largest aggressive-scenario annual savings, in US dollars.</summary>
    public const long AggressiveSavingsMax = 10_000_000;
}
